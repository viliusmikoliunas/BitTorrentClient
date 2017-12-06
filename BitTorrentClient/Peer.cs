using System;
using System.Linq;
using System.Net;

namespace BitTorrentClient
{
    public class Peer
    {
        public IPAddress IpAddress { get; }
        public ushort Port { get; }
        public bool[] PiecesArray { get; }

        private int _currentIndexOfPiece = -1;
        public int IndexOfNextAvaivablePiece => GetNextAvaivablePieceIndex();

        private int _pieceCounter = 0;
        public bool CurrentPiece
        {
            set
            {
                try
                {
                    PiecesArray[_pieceCounter++] = value;
                }
                catch (IndexOutOfRangeException) { }
            } 
        }

        public Peer(byte[] ipBytes, byte[] portBytes, int pieceCount)
        {
            var tempip = new IPAddress(ipBytes);
            if (tempip.Equals(IPAddress.Parse(Utilities.ownIpAddress))) throw new ArgumentException();

            IpAddress = tempip;
            Port = BitConverter.ToUInt16(portBytes.Reverse().ToArray(), 0);
            PiecesArray = new bool[pieceCount];
        }

        private int GetNextAvaivablePieceIndex()
        {
            if (_currentIndexOfPiece == -1 && PiecesArray[0])
            {
                _currentIndexOfPiece = 0;
                return 0;
            }

            for (int i = _currentIndexOfPiece + 1; i < PiecesArray.Length; i++)
            {
                if (PiecesArray[i])
                {
                    _currentIndexOfPiece = i;
                    return i;
                }
            }
            return -1;
        }
    }
}
