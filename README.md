# BitTorrentClient
Project on implementing BitTorrent protocol.
This client is able to download a single file.
Metadata must be provided by .torrent file.
This program deals only with udp:// trackers so downloading performance depends on number of udp trackers provided by metadata.
.torrent file reading is supported by BencodeNET: .NET library for encoding and decoding bencode (link:https://github.com/Krusen/BencodeNET)
