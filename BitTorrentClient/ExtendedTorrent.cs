using BencodeNET.Parsing;
using BencodeNET.Torrents;

namespace BitTorrentClient
{

    public class ExtendedTorrent : Torrent
    {
        #region fields from Torrent class rewritten to have setters
        public new string DisplayName { get; private set; }
        public new TorrentFileMode FileMode { get; private set; }
        public new int NumberOfPieces { get; private set; }
        public new long TotalSize { get; private set; }
        #endregion

        public readonly string PeerId = Utilities.GeneratePeerId();

        public ExtendedTorrent(string filePath)
        {
            var parser = new BencodeParser();
            var torrent = parser.Parse<Torrent>(filePath);
            CopyFields(torrent);
        }

        private void CopyFields(Torrent torrent)
        {
            Comment = torrent.Comment;
            CreatedBy = torrent.CreatedBy;
            CreationDate = torrent.CreationDate;
            DisplayName = torrent.DisplayName;
            Encoding = torrent.Encoding;
            ExtraFields = torrent.ExtraFields;
            File = torrent.File;
            FileMode = torrent.FileMode;
            Files = torrent.Files;
            IsPrivate = torrent.IsPrivate;
            NumberOfPieces = torrent.NumberOfPieces;
            PieceSize = torrent.PieceSize;
            PiecesAsHexString = torrent.PiecesAsHexString;
            Pieces = torrent.Pieces;
            TotalSize = torrent.TotalSize;
            Trackers = torrent.Trackers;
        }
    }
}
