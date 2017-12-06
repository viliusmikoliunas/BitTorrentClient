using System;
using System.IO;
using System.Linq;

namespace BitTorrentClient
{
    //class that handles file saving by pieces
    public class FileSavingController
    {
        private static string downloadPath = @"D:\TESTAS";
        private string pieceFolderName = "temp";
        private string pieceFileNameTemplate = "Piece";
        private char pieceNumberSign = '#';
        private char numberRangeSeparator = '-';
        private string fileExtension = ".dat";
        private readonly string fileName;
        public bool[] DownloadedPieces { get; }

        public FileSavingController(ExtendedTorrent torrent)
        {
            DownloadedPieces = new bool[torrent.NumberOfPieces];
            MarkAlreadyDownloadedPieces();
            fileName = torrent.File.FileName;
        }

        private void MarkAlreadyDownloadedPieces()
        {
            string piecesPath = downloadPath + Path.DirectorySeparatorChar + pieceFolderName;
            if (!Directory.Exists(piecesPath))
            {
                Directory.CreateDirectory(piecesPath);
                return;
            }
            var fileList = Directory.GetFiles(piecesPath);
            foreach (var file in fileList)
            {
                int lastIndex;
                int firstIndex = GetIndexesFromFilename(file, out lastIndex);
                if (lastIndex == -1) DownloadedPieces[firstIndex] = true;
                else
                {
                    for (int i = firstIndex; i <= lastIndex; i++)
                        DownloadedPieces[i] = true;
                }
            }
        }

        private int GetIndexesFromFilename(string path, out int secondIndex)
        {
            int firstIndex = GetFirstIndexFromFileName(path);
            if (path.Count(c => c == pieceNumberSign) == 2)
                secondIndex = GetSecondIndexFromFileName(path);
            else secondIndex = -1;
            return firstIndex;
        }

        public void PiecesToSingleFile()
        {
            if (!DownloadedPieces.All(c => c)) return;

            var fileList = Directory.GetFiles(downloadPath + Path.DirectorySeparatorChar + pieceFolderName).ToList();
            string fullPath = downloadPath + Path.DirectorySeparatorChar + fileName;
            using (var fileStream = new FileStream(fullPath, FileMode.Append, FileAccess.Write))
            {
                int indexOfFileToWriteFrom = 0;
                while(fileList.Count>0)
                {
                    string path = fileList.Single(c => c.Contains(pieceNumberSign + indexOfFileToWriteFrom.ToString()+numberRangeSeparator));
                    int numberOfPieces;
                    if (path.Count(c => c == pieceNumberSign) == 2)
                        numberOfPieces = GetSecondIndexFromFileName(path) - indexOfFileToWriteFrom + 1;
                    else numberOfPieces = 1;

                    using (var mainStream = new FileStream(path,FileMode.Open,FileAccess.Read))
                    {
                        byte[] fileBytes = new byte[numberOfPieces*Utilities.blockSize];
                        mainStream.Read(fileBytes, 0, fileBytes.Length);

                        fileStream.Write(fileBytes,0,fileBytes.Length);
                    }
                    indexOfFileToWriteFrom += numberOfPieces;
                    fileList.Remove(path);
                }
            }
            Directory.Delete(downloadPath+Path.DirectorySeparatorChar+pieceFolderName,true);
        }

        private int GetFirstIndexFromFileName(string path)
        {
            string filename = path.Substring(path.LastIndexOf(Path.DirectorySeparatorChar) + 1);
            int indexOfFirstDigitInString = filename.IndexOf(pieceNumberSign) + 1;
            int lengthOfNumberInFilename = filename.IndexOf(numberRangeSeparator) - indexOfFirstDigitInString;
            int index;
            try
            {
                index =  Convert.ToInt32(filename.Substring(indexOfFirstDigitInString, lengthOfNumberInFilename));
            }
            catch (ArgumentOutOfRangeException)
            {
                lengthOfNumberInFilename = filename.IndexOf('.') - indexOfFirstDigitInString;
                index = Convert.ToInt32(filename.Substring(indexOfFirstDigitInString, lengthOfNumberInFilename));
            }
            return index;
        }

        private int GetSecondIndexFromFileName(string path)
        {
            string filename = path.Substring(path.LastIndexOf(Path.DirectorySeparatorChar) + 1);
            int indexInString2 = filename.LastIndexOf(pieceNumberSign);
            int endIndex = Convert.ToInt32(filename.Substring(indexInString2 + 1,
                filename.LastIndexOf('.') - indexInString2 - 1));

            return endIndex;
        }

        public void SavePiece(byte[] piece, int index)
        {
            try
            {
                if (DownloadedPieces[index]) return;

                if (DownloadedPieces[index - 1]) AppendPieceToExistingFile(piece, index);
                else SavePieceToNewFile(piece, index);
            }
            catch (IndexOutOfRangeException)//in case of index=0
            {
                SavePieceToNewFile(piece, index);
            }
            DownloadedPieces[index] = true;
        }

        private void AppendPieceToExistingFile(byte[] piece, int index)
        {
            string directory = downloadPath + Path.DirectorySeparatorChar + pieceFolderName;
            var files = Directory.GetFiles(directory);
            var fileToAppendTo = files.Single(c => c.Contains(pieceNumberSign+(index - 1).ToString()+fileExtension));

            using (var stream = new FileStream(fileToAppendTo, FileMode.Append, FileAccess.Write))
            {
                stream.Write(piece,0,piece.Length);
            }

            if(fileToAppendTo.Count(c=>c==pieceNumberSign)==1) AppendIndexToFileName(fileToAppendTo,index);
            else ChangeIndexInFileName(fileToAppendTo,index);
        }

        private void AppendIndexToFileName(string fileFullPath, int index)
        {
            string newFullPath = fileFullPath.Replace(fileExtension, "");
            newFullPath += numberRangeSeparator.ToString() + pieceNumberSign + index + fileExtension;
            ChangeFileName(fileFullPath, newFullPath);
        }

        private void ChangeFileName(string oldFilenameFullPath, string newFilenameFullPath)
        {
            File.Move(oldFilenameFullPath, newFilenameFullPath);
            File.Delete(oldFilenameFullPath);
        }

        private void ChangeIndexInFileName(string oldFullPath, int index)//Piece#1-#3.dat -> Piece#1-#4.dat
        {
            int indexOfLastSeparator = oldFullPath.LastIndexOf(Path.DirectorySeparatorChar);
            string fileNameToChange = oldFullPath.Substring(indexOfLastSeparator + 1);
            string newFileName = string.Copy(fileNameToChange);
            newFileName = newFileName.Replace((index - 1).ToString(), index.ToString());
            string newFullPath = oldFullPath.Replace(fileNameToChange, newFileName);
            ChangeFileName(oldFullPath,newFullPath);
        }

        private void SavePieceToNewFile(byte[] piece, int index)
        {
            string path = downloadPath + Path.DirectorySeparatorChar + pieceFolderName;
            string newFileName = pieceFileNameTemplate + pieceNumberSign + index + fileExtension;
            string fullPath = path + Path.DirectorySeparatorChar + newFileName;

            using (var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
            {
                stream.Write(piece,0,piece.Length);
            }
        }

    }
}
