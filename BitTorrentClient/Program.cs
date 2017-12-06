using System;
using System.Collections.Generic;

namespace BitTorrentClient
{
    class Program
    {
        static void Main(string[] args)
        {
            string weekendmp3 = @"D364D9E12EF2B8C4FF44BB7E36E749126C119E2D.torrent";//*full .torrent file path is needed here
            var torrent = new ExtendedTorrent(weekendmp3);
            var peerList = GetPeerList(torrent, Utilities.listeningPort);
            Download(peerList, torrent);
        }
        
        private static List<Peer> GetPeerList(ExtendedTorrent torrent, int listeningPort)
        {
            var peerList = new List<Peer>();
            foreach (var tracker in torrent.Trackers)
            {
                string trackerAddress = tracker[0];//tracker's address is nested in a list within a list
                Console.WriteLine(trackerAddress);
                if (trackerAddress.Contains("udp://"))
                {
                    byte[] trackerResponse =
                        UdpProtolocHandler.UdpTrackerResponse(trackerAddress, torrent, listeningPort, torrent.PeerId);
                    if (trackerResponse != null)
                    {
                        var trackerPeers = UdpProtolocHandler.GetPeerListFromUdpTrackerResponse(trackerResponse,torrent);
                        if(trackerPeers.Count>0) peerList.AddRange(trackerPeers);
                        Console.WriteLine("IPs collected:" + trackerPeers.Count);
                    }
                    else
                    {
                        Console.WriteLine("Failed collect IP's");
                    }
                }
                else
                {
                    Console.WriteLine("Unsupported protocol");
                }
                Console.WriteLine();
            }
            return peerList;
        }
        
        private static void Download(List<Peer> peerList, ExtendedTorrent torrent)
        {
            FileSavingController fileSavingController = new FileSavingController(torrent);
            while (true)
            {
                foreach (var peer in peerList)
                {
                    int success = PeerWireProtocolHandler.ConnectToPeer(peer, torrent, torrent.PeerId, fileSavingController);
                    Console.WriteLine(peer.IpAddress + " " + peer.Port);
                    Console.WriteLine("Error:"+GetErrorMessage(success));

                    if (success == 100)
                    {
                        Environment.Exit(0);
                    }
                }
            }
        }

        private static string GetErrorMessage(int code)
        {
            switch (code)
            {
                case 1:
                    return "Peer answer too short";
                case 2:
                    return "Peer has not implemented BitTorrentProtocol";
                case 3:
                    return "Peer sent incorrect infohash";
                case 4:
                    return "Unsupported message type received";
                case 5:
                    return "Peer does not want to send data";

                case 10:
                    return "Peer has no pieces left to offer";
                case 11:
                    return "Piece hashes do not match";

                case -2:
                    return "Connection timed out";
                case 100:
                    return "Download completed";

                default:
                    return "Failed to reach Peer";
            }
        }
    }
}
