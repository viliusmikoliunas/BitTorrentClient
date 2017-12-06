using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BitTorrentClient
{
    internal static class Utilities
    {
        internal static int listeningPort = 6881;
        internal static string ownIpAddress = "";
        internal static int blockSize = 16384;//recommended size 2^14 B = 16KB

        public static T[] SubArray<T>(this T[] data, int index, int length)
        {
            var result = new T[length];
            Array.Copy(data, index, result, 0, length);
            return result;
        }

        public static string GeneratePeerId()
        {
            Random random = new Random();
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            string id = "-BN0100-";//azureus style
            return id + new string(Enumerable.Repeat(chars, 20 - id.Length)
                       .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        public static string ByteArrayToString(byte[] ba, string separator = "-")
        {
            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
                hex.AppendFormat("{0:x2}" + separator, b);
            string answer = hex.ToString();
            return answer.Substring(0, answer.Length - 1);
        }

        public static void PeerListToFile(List<Peer> peerList, string fileName = "PeerList.txt")
        {
            using (var writer = new StreamWriter("../../TextFiles/"+fileName))
            {
                foreach (var peer in peerList)
                    writer.WriteLine(peer.IpAddress + " " + peer.Port);
            }
        }
    }
}
