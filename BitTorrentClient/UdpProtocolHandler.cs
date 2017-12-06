using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

//UDP protocol in torrents info http://www.bittorrent.org/beps/bep_0015.html
namespace BitTorrentClient
{
    public static class UdpProtolocHandler
    {
        private const int TimeTolerance = 1;//seconds to wait for answer

        public static byte[] UdpTrackerResponse(string trackerAddress, ExtendedTorrent torrent, int listeningPort, string peerId)
        {
            int trackerPort;
            string strippedAddress = SplitTrackerAddress(trackerAddress, out trackerPort);
            if (trackerPort == -1) return null;

            byte[] transactionId = BitConverter.GetBytes(new Random().Next(0, int.MaxValue));
            byte[] connectionid = GetConnectionId(strippedAddress, trackerPort, transactionId,listeningPort);
            if (connectionid == null) return null;

            byte[] response = GetAnnounceResponse(strippedAddress, trackerPort, connectionid, transactionId, torrent, listeningPort,peerId);
            int err = ValidateAnnounceResponse(response, transactionId);
            if (err != 0) return null;
            return response;
        }

        private static int ValidateAnnounceResponse(byte[] response, byte[] transactionId)
        {
            if (response == null || response.Length < 20) return 1;

            byte[] responseTransactionId = response.SubArray(4, 4);
            if (!responseTransactionId.SequenceEqual(transactionId)) return 2;

            byte[] actionBytes = response.SubArray(0, 4);
            if (BitConverter.ToInt32(actionBytes.Reverse().ToArray(), 0) != 1) return 3;

            return 0;
        }
        private static byte[] GetAnnounceResponse(string trackerAddress, int trackerPort, byte[] connectionId, byte[] transactionId, ExtendedTorrent torrent, int listeningPort, string peerId)
        {
            byte[] requestPacket = GetAnnounceRequestPacketZero(connectionId, transactionId, torrent, listeningPort,peerId);

            using (var udpClient = new UdpClient(listeningPort))
            {
                var timeToWait = TimeSpan.FromSeconds(TimeTolerance);
                udpClient.Connect(trackerAddress, trackerPort);
                udpClient.Send(requestPacket, requestPacket.Length);
                var asyncResult = udpClient.BeginReceive(null, null);
                asyncResult.AsyncWaitHandle.WaitOne(timeToWait);
                if (!asyncResult.IsCompleted) return null;
                try
                {
                    IPEndPoint remoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);
                    byte[] receiveBytes = udpClient.EndReceive(asyncResult, ref remoteIpEndPoint);
                    return receiveBytes;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }
        /*TBC
        public static byte[] GetAnnounceRequestPacket(byte[] connectionId, byte[] transactionId, Torrent torrent, int localPort)
        {
            byte[] announcePacket = new byte[98];
            Array.Copy(connectionId, 0, announcePacket, 0, 8);
            Array.Copy(BitConverter.GetBytes(1).Reverse().ToArray(), 0, announcePacket, 8, 4);//action
            Array.Copy(transactionId, 0, announcePacket, 12, 4);
            Array.Copy(torrent.GetInfoHashBytes(), 0, announcePacket, 16, 20);
            Array.Copy(Encoding.ASCII.GetBytes(GeneratePeerId()), 0, announcePacket, 36, 20);
            Array.Copy(BitConverter.GetBytes(torrent).Reverse().ToArray(), 0, announcePacket, 56, 8);//downloaded
            Array.Copy(BitConverter.GetBytes(torrent.Left).Reverse().ToArray(), 0, announcePacket, 64, 8);//left
            Array.Copy(BitConverter.GetBytes(torrent.Uploaded).Reverse().ToArray(), 0, announcePacket, 72, 8);//uploaded

            //4bytes: event (0)  + 4bytes: ip addr (0)
            Array.Copy(BitConverter.GetBytes((long)0), 0, announcePacket, 80, 8);
            byte[] key = BitConverter.GetBytes(new Random().Next(0, int.MaxValue)).Reverse().ToArray();
            Array.Copy(key, 0, announcePacket, 88, 4);
            Array.Copy(BitConverter.GetBytes(-1).Reverse().ToArray(), 0, announcePacket, 92, 4);
            Array.Copy(BitConverter.GetBytes((ushort)localPort).Reverse().ToArray(), 0, announcePacket, 96, 2);
            return announcePacket;
        }*/
        
        private static byte[] GetAnnounceRequestPacketZero(byte[] connectionId, byte[] transactionId, ExtendedTorrent torrent, int localPort, string peerId)
        {
            byte[] announcePacket = new byte[98];
            Array.Copy(connectionId, 0, announcePacket, 0, 8);
            Array.Copy(BitConverter.GetBytes(1).Reverse().ToArray(), 0, announcePacket, 8, 4);//action 1 - announce
            Array.Copy(transactionId, 0, announcePacket, 12, 4);
            Array.Copy(torrent.GetInfoHashBytes(), 0, announcePacket, 16, 20);
            Array.Copy(Encoding.ASCII.GetBytes(peerId), 0, announcePacket, 36, 20);
            Array.Copy(BitConverter.GetBytes((long)0), 0, announcePacket, 56, 8);//downloaded
            Array.Copy(BitConverter.GetBytes(torrent.TotalSize).Reverse().ToArray(), 0, announcePacket, 64, 8);//left
            Array.Copy(BitConverter.GetBytes((long)0), 0, announcePacket, 72, 8);//event 0-none
            //4bytes: event (0)  + 4bytes: ip addr (0)
            Array.Copy(BitConverter.GetBytes((long)0), 0, announcePacket, 80, 8);
            byte[] key = BitConverter.GetBytes(new Random().Next(0, int.MaxValue)).Reverse().ToArray();
            Array.Copy(key, 0, announcePacket, 88, 4);
            Array.Copy(BitConverter.GetBytes(-1), 0, announcePacket, 92, 4);//default
            Array.Copy(BitConverter.GetBytes((ushort)localPort).Reverse().ToArray(), 0, announcePacket, 96, 2);
            return announcePacket;
        }

        private static byte[] GetConnectionId(string trackerAddress, int trackerPort, byte[] transactionId, int listeningPort)
        {
            byte[] connectionPacket = GetConnectionPacket(transactionId);
            using (var udpClient = new UdpClient(listeningPort))
            {
                try
                {
                    udpClient.Connect(trackerAddress, trackerPort);
                }
                catch (Exception)
                {
                    return null;
                }
                udpClient.Send(connectionPacket, connectionPacket.Length);
                //timeout stuff
                var timeToWait = TimeSpan.FromSeconds(TimeTolerance);
                var asyncResult = udpClient.BeginReceive(null, null);
                asyncResult.AsyncWaitHandle.WaitOne(timeToWait);
                if (!asyncResult.IsCompleted) return null;
                try
                {
                    IPEndPoint remoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);
                    byte[] receiveBytes = udpClient.EndReceive(asyncResult, ref remoteIpEndPoint);
                    int err = ValidateConnectResponse(receiveBytes, transactionId);
                    if (err != 0) return null;
                    return receiveBytes.SubArray(8, 8);
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        private static int ValidateConnectResponse(byte[] response, byte[] transactionId)
        {
            if (response.Length < 16) return 1;

            byte[] receivedTransactionId = new byte[4];
            Array.Copy(response, 4, receivedTransactionId, 0, 4);
            if (!receivedTransactionId.SequenceEqual(transactionId)) return 2;//could retry asking connectionId

            byte[] receivedActionId = new byte[4];
            Array.Copy(response, 0, receivedActionId, 0, 4);
            if (BitConverter.ToInt32(receivedActionId, 0) != 0) return 3; //if action is not connect (can be #3 for errors)
            return 0;
        }

        private static string SplitTrackerAddress(string trackerAddress, out int port)
        {
            string strippedTrackerAddress = trackerAddress.Replace("udp://", "");
            strippedTrackerAddress = strippedTrackerAddress.Replace("/announce", "");
            try
            {
                //port str = colon with port number e.g. ":6881"
                string portStr =
                    strippedTrackerAddress.Substring(strippedTrackerAddress.LastIndexOf(":", StringComparison.Ordinal),
                        strippedTrackerAddress.Length - strippedTrackerAddress.LastIndexOf(":", StringComparison.Ordinal));

                strippedTrackerAddress = strippedTrackerAddress.Replace(portStr, "");
                port = Convert.ToInt32(portStr.Substring(1, portStr.Length - 1));//remove colon form portStr
            }
            catch (Exception)
            {
                port = -1;
            }
            return strippedTrackerAddress;
        }

        private static byte[] GetConnectionPacket(byte[] transactionId)
        {
            byte[] connectionPacket = new byte[16];
            byte[] protocolId = BitConverter.GetBytes(0x41727101980).Reverse().ToArray();//0x41727101980 "magic const"
            byte[] actionId = BitConverter.GetBytes(0);
            Array.Copy(protocolId, 0, connectionPacket, 0, 8);
            Array.Copy(actionId, 0, connectionPacket, 8, 4);
            Array.Copy(transactionId, 0, connectionPacket, 12, 4);
            return connectionPacket;
        }

        public static List<Peer> GetPeerListFromUdpTrackerResponse(byte[] trackerResponse, ExtendedTorrent torrent)
        {
            var peerList = new List<Peer>();
            for (int i = 20; i < trackerResponse.Length; i += 6)
            {
                var ipBytes = trackerResponse.SubArray(i, 4);
                var portBytes = trackerResponse.SubArray(i+4, 2);

                try
                {
                    Peer peer = new Peer(ipBytes, portBytes,torrent.NumberOfPieces);
                    peerList.Add(peer);
                }
                catch (ArgumentException) { }
            }
            return peerList;
        }

        public static string ParseUdpTrackerError(byte[] trackerResponse)
        {
            int errorMessageLength = trackerResponse.Length - 8;//4 bytes - action + 4 bytes transactionid
            byte[] errorMessageBytes = new byte[errorMessageLength];
            Array.Copy(trackerResponse, 8, errorMessageBytes, 0, errorMessageLength);
            return Encoding.Default.GetString(errorMessageBytes);
        }
    }
}