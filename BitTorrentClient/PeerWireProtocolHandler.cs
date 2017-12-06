using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;


//https://wiki.theory.org/BitTorrentSpecification#Handshake
namespace BitTorrentClient
{
    public static class PeerWireProtocolHandler
    {
        private struct BitTorrentProtocol
        {
            private const string ProtocolName = "BitTorrent protocol";
            public const int ProtocolNameLength = 19;
            public static readonly byte[] ProtocolNameBytes = Encoding.ASCII.GetBytes(ProtocolName);
        }

        private static readonly int _peerTolerance = 1;

        public static int ConnectToPeer(Peer peer, ExtendedTorrent torrent, string peerId, FileSavingController fileSavingController)
        {
            byte[] handshakeMessage = GetPeerHandshakeMessage(torrent, peerId);
            using (var tcpClient = new TcpClient())
            {
                try
                {
                    var result = tcpClient.BeginConnect(peer.IpAddress, peer.Port, null, null);
                    var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(_peerTolerance));
                    if (!success) return -1;
                    using (var stream = tcpClient.GetStream())
                    {
                        //handshaking process
                        stream.Write(handshakeMessage, 0, handshakeMessage.Length);
                        byte[] response = new byte[512];
                        int responseLength = stream.Read(response, 0, response.Length);
                        int validationErr = ValidatePeerHandshake(response, responseLength, torrent);
                        if (validationErr != 0) return validationErr;

                        int responseWithoutHandshakeLength =
                            responseLength - 49 - BitTorrentProtocol.ProtocolNameLength;
                        byte[] responseWithoutHandshake = response.SubArray(49 + BitTorrentProtocol.ProtocolNameLength,
                            responseWithoutHandshakeLength);

                        //handle bitfield message
                        int responseId = GetMessageId(responseWithoutHandshake);
                        switch (responseId)
                        {
                            case -1:
                                return -1;
                            case 5:
                                HandleBitfieldMessage(responseWithoutHandshake, responseWithoutHandshakeLength, peer, stream);
                                break;
                            default:
                                return 4;
                        }
                        //Send interested: <len=0001><id=2>
                        byte[] interested = new byte[5];
                        Array.Copy(BitConverter.GetBytes(1).Reverse().ToArray(),0,interested,0,4);
                        interested[4] = Convert.ToByte(2);
                        stream.Write(interested, 0, 5);

                        //receive unchoke: <len=0001><id=1>
                        byte[] potentialUnchoke = new byte[5];
                        stream.Read(potentialUnchoke, 0, 5);
                        int len = BitConverter.ToInt32(potentialUnchoke.SubArray(0, 4).Reverse().ToArray(), 0);
                        int id = Convert.ToInt32(potentialUnchoke[4]);
                        if (len != 1 || id != 1) return 5;

                        //4.request for piece 

                        //set index of next piece to download
                        int index = GetIndexOfNextPieceAvaivable(peer, fileSavingController);
                        if (index == -1) return 10;

                        //downloading sequence
                        int consecutiveFails = 0;
                        while(consecutiveFails<7)
                        {
                            byte[] piece = DownloadPiece(index, torrent, stream,fileSavingController);
                            if (piece == null)
                            {
                                Console.WriteLine(DateTime.Now + "||Piece #" + index + " Failed to download");
                                consecutiveFails++;
                            }
                            else if (BitConverter.ToInt32(piece.SubArray(0,4),0) == -1) return -2;
                            else
                            {
                                bool hashesMatch = CheckIfHashesMatch(piece, torrent, index);
                                if (hashesMatch)
                                {
                                    fileSavingController.SavePiece(piece, index);

                                    Console.WriteLine(DateTime.Now + " | Downloaded piece #" + index);
                                    var compl = fileSavingController.DownloadedPieces.Where(c => c).Count();
                                    Console.WriteLine("Downloaded " + compl + "/" +
                                                      fileSavingController.DownloadedPieces.Length + " pieces");

                                    if (compl == fileSavingController.DownloadedPieces.Length)
                                    {
                                        fileSavingController.PiecesToSingleFile();
                                        return 100;
                                    }
                                }
                                else
                                {
                                    return 11;
                                }
                            }

                            index = GetIndexOfNextPieceAvaivable(peer, fileSavingController);
                            if (index == -1) return 10;
                        }
                        return -1;

                    }
                }
                catch (IOException)
                {
                    return -1;
                }
            }
        }

        private static int GetIndexOfNextPieceAvaivable(Peer peer, FileSavingController fileSavingController)
        {
            int index;
            do
            {
                index = peer.IndexOfNextAvaivablePiece;
                if (index == -1) return -1;//peer has no more pieces left to offer
            } while (fileSavingController.DownloadedPieces[index]);
            return index;
        }

        private static bool CheckIfHashesMatch(byte[] piece, ExtendedTorrent torrent, int indexOfPiece)
        {
            byte[] localHash = torrent.Pieces.SubArray(indexOfPiece*20,20);
            byte[] receivedHash = new SHA1Managed().ComputeHash(piece);
            return localHash.SequenceEqual(receivedHash);
        }
        
        private static byte[] DownloadPiece(int index, ExtendedTorrent torrent, NetworkStream stream, FileSavingController fileSavingController)
        {
            long sizeOfPiece = torrent.PieceSize;

            if (fileSavingController.DownloadedPieces.Length - 1 == index)//last package special
            {
                long lastPackageLengthDifference = torrent.NumberOfPieces * torrent.PieceSize -
                                                   torrent.TotalSize;
                sizeOfPiece = torrent.PieceSize - lastPackageLengthDifference;
            }

            int sizeOfRequestedBlock = Utilities.blockSize;
            byte[] piece = new byte[sizeOfPiece];

            for (int pieceOffset = 0; pieceOffset < sizeOfPiece; pieceOffset += sizeOfRequestedBlock)
            {
                int requestLength = sizeOfPiece - pieceOffset < sizeOfRequestedBlock
                    ? (int)(sizeOfPiece - pieceOffset)
                    : sizeOfRequestedBlock;//last block size will be smaller than the rest of
                byte[] requestMessage = GetNextRequestMessage(index, pieceOffset, requestLength);
                stream.Write(requestMessage, 0, requestMessage.Length);

                byte[] response = new byte[requestLength+13];//13 = 4b len + 1b id + 4b index + 4b begin
                try
                {
                    stream.ReadTimeout = _peerTolerance * 1000;
                    int responseLength = stream.Read(response, 0, response.Length);
                    if (responseLength == 0)
                    {
                        Console.WriteLine("Response Length 0");
                        return null;
                    }
                    int err = ValidatePieceMessage(response, requestLength, index, pieceOffset);
                    if (err != 0)
                    {
                        //Console.WriteLine("Fail code:"+err);
                        return null;
                    }
                    Array.Copy(response, 13, piece, pieceOffset, requestLength);
                }
                catch (IOException)
                {
                    Console.WriteLine("Connection timed out");
                    return BitConverter.GetBytes(-1);
                }

            }
            return piece;
        }

        private static int ValidatePieceMessage(byte[] message, int requestedBlockLength, int indexOfPiece, int offsetSent)
        {                
            //piece: <len=0009+X><id=7><index><begin><block>
            int len = BitConverter.ToInt32(message.SubArray(0, 4).Reverse().ToArray(), 0) - 9;
            if (len != requestedBlockLength) return 1;

            int id = Convert.ToInt32(message[4]);
            if (id != 7) return 2;

            int index = BitConverter.ToInt32(message.SubArray(5, 4).Reverse().ToArray(), 0);
            if (index != indexOfPiece)
            {
                return 3;
            }


            int offset = BitConverter.ToInt32(message.SubArray(9, 4).Reverse().ToArray(), 0);
            if (offset != offsetSent) return 4;

            return 0;
        }
        private static byte[] GetNextRequestMessage(int index, int begin, int length)
        {
            //<len=0013><id=6><index><begin><length> length=16384
            byte[] requestMessage = new byte[17];
            Array.Copy(BitConverter.GetBytes(13).Reverse().ToArray(),0,requestMessage,0,4);
            requestMessage[4] = Convert.ToByte(6);
            Array.Copy(BitConverter.GetBytes(index).Reverse().ToArray(),0,requestMessage,5,4);
            Array.Copy(BitConverter.GetBytes(begin).Reverse().ToArray(),0,requestMessage,9,4);
            Array.Copy(BitConverter.GetBytes(length).Reverse().ToArray(),0,requestMessage,13,4);
            return requestMessage;
        }
        
        private static int HandleBitfieldMessage(byte[] response, int responseLength, Peer peer, NetworkStream stream)
        {
            int bytesLeftToReceive;
            ParseBitfieldMessage(response, responseLength, peer, out bytesLeftToReceive);

            if (bytesLeftToReceive > 0)
            {
                //finish handling bitfield
                byte[] secondResponse = new byte[512];
                int secondResponseLength = stream.Read(secondResponse, 0, secondResponse.Length);
                byte[] bitfieldMessagePart = secondResponse.SubArray(0, bytesLeftToReceive);
                BitArray secondBitfieldBits = new BitArray(bitfieldMessagePart);
                foreach (bool bit in secondBitfieldBits) peer.CurrentPiece = bit;

                //handle have messages
                int haveMessageLength = secondResponseLength - bytesLeftToReceive;
                byte[] haveMessages = secondResponse.SubArray(bytesLeftToReceive, haveMessageLength);
                int err = HandleAllHaveMessages(haveMessages, haveMessageLength, peer);
                return err;
            }
            return 0;
        }

        private static int HandleAllHaveMessages(byte[] haveMessages, int haveMessagesLength, Peer peer)
        {
            int i = 0;
            for (; i < haveMessagesLength; i += 9)
            {
                byte[] currentHaveMessage = haveMessages.SubArray(i, 9);
                int err = HandleHaveMessage(currentHaveMessage, peer);
                if (err != 0) return err;
            }
            return i == haveMessagesLength ? 0 : 1;
        }

        private static int HandleHaveMessage(byte[] message, Peer peer)
        {
            //have: <len=0005><id=4><piece index> 4+1+4 bytes
            int messageLength = BitConverter.ToInt32(message.SubArray(0, 4), 0);
            if (messageLength != 5) return 1;

            int messageId = Convert.ToInt32(message[4]);
            if (messageId != 4) return 2;

            int pieceIndex = BitConverter.ToInt32(message.SubArray(5, 4), 0);
            peer.PiecesArray[pieceIndex] = true;
            return 0;
        }

        private static void ParseBitfieldMessage(byte[] bitfieldMsg, int bitFieldLength, Peer peer, out int bitfieldBytesLeftToReceive)
        {
            byte[] bitfieldBytes = bitfieldMsg.SubArray(5, bitFieldLength - 5);
            int bitFieldTotalMessageLength = BitConverter.ToInt32(bitfieldMsg.SubArray(0, 4).Reverse().ToArray(), 0) - 1;
            BitArray bitFieldBits = new BitArray(bitfieldBytes);
            foreach (bool bit in bitFieldBits) peer.CurrentPiece = bit;
            bitfieldBytesLeftToReceive = bitFieldTotalMessageLength - bitfieldBytes.Length;
        }

        private static int GetMessageId(byte[] message)
        {
            try
            {
                return Convert.ToInt32(message[4]);
            }
            catch (IndexOutOfRangeException)
            {
                return -1;
            }
        }

        private static int ValidatePeerHandshake(byte[] response, int responseLength, ExtendedTorrent torrent)
        {
            int peerPstrLength = Convert.ToInt32(response[0]);
            if (responseLength < peerPstrLength + 49) return 1; //possible keep alive message

            if (peerPstrLength != BitTorrentProtocol.ProtocolNameLength) return 2;
            byte[] peerProtocolNamebytes = response.SubArray(1, peerPstrLength);
            if (!peerProtocolNamebytes.SequenceEqual(BitTorrentProtocol.ProtocolNameBytes))
                return 2; //not BitTorrentProtocol

            byte[] receivedInfoHash = response.SubArray(peerPstrLength + 9, 20);
            if (!receivedInfoHash.SequenceEqual(torrent.GetInfoHashBytes())) return 3; //bad info hash
            return 0; //handshake correct
        }

        private static byte[] GetPeerHandshakeMessage(ExtendedTorrent torrent, string peerId)
        {
            byte[] handshake = new byte[49 + BitTorrentProtocol.ProtocolNameLength];
            handshake[0] = Convert.ToByte(BitTorrentProtocol.ProtocolNameLength);
            Array.Copy(BitTorrentProtocol.ProtocolNameBytes, 0, handshake, 1, BitTorrentProtocol.ProtocolNameLength);
            Array.Copy(BitConverter.GetBytes((long) 0), 0, handshake, BitTorrentProtocol.ProtocolNameLength + 1,
                8); //reserved
            Array.Copy(torrent.GetInfoHashBytes(), 0, handshake, BitTorrentProtocol.ProtocolNameLength + 9, 20);
            Array.Copy(Encoding.ASCII.GetBytes(peerId), 0, handshake, BitTorrentProtocol.ProtocolNameLength + 29, 20);
            return handshake;
        }

    }
}
