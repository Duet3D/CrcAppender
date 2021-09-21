using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CrcAppender
{
    /// <summary>
    /// Functions to an embedded filesystem structure for RRF
    /// </summary>
    public static class FS
    {
        /// <summary>
        /// Magic number that each FS image starts with
        /// </summary>
        private const uint Magic = 0x543C2BEF;

        /// <summary>
        /// Convert an filename e.g. from C:\SD\sys\foo.txt to /sys/foo.txt and return it as UTF-8 bytes
        /// </summary>
        /// <param name="filename">Filename to convert</param>
        /// <param name="rootDirectory">Root directory</param>
        /// <returns>Converted filename</returns>
        private static byte[] ConvertFilename(string filename, string rootDirectory)
        {
            string result = filename.Substring(rootDirectory.EndsWith(Path.DirectorySeparatorChar) ? rootDirectory.Length : rootDirectory.Length + 1);
            return Encoding.UTF8.GetBytes('/' + result.Replace(Path.DirectorySeparatorChar, '/'));
        }

        /// <summary>
        /// Get all directories and sub-directories from the given directory
        /// </summary>
        /// <param name="directory">Directory to search in</param>
        /// <returns>List of all sub-directories</returns>
        private static List<string> GetDirectories(string directory)
        {
            List<string> result = new();
            foreach (string subDirectory in Directory.EnumerateDirectories(directory))
            {
                result.Add(subDirectory);
                result.AddRange(GetDirectories(subDirectory));
            }
            return result;
        }

        /// <summary>
        /// Pseudo-class to hold all attributes 
        /// </summary>
        private class FileEntry
        {
            /// <summary>
            /// Dummy for an empty file entry
            /// </summary>
            public static readonly byte[] Empty = new byte[sizeof(uint) * 3];

            /// <summary>
            /// Constructor of this class
            /// </summary>
            /// <param name="filename">Filename</param>
            /// <param name="length">Length in bytes</param>
            public FileEntry(string filename)
            {
                Filename = filename;
            }

            /// <summary>
            /// Filename
            /// </summary>
            public string Filename { get; }

            /// <summary>
            /// Offset of the filename
            /// </summary>
            public uint NameOffset { get; set; }

            /// <summary>
            /// Offset of the content
            /// </summary>
            public uint ContentOffset { get; set; }

            /// <summary>
            /// Length of the content
            /// </summary>
            public uint ContentLength { get; set; }
        }

        /// <summary>
        /// Get all files from the given root directory and directories
        /// </summary>
        /// <param name="rootDirectory">Root directory</param>
        /// <param name="directories">Directories</param>
        /// <returns>List of file entries</returns>
        private static List<FileEntry> GetFileEntries(string rootDirectory, IEnumerable<string> directories)
        {
            List<FileEntry> result = new();
            foreach (string filename in Directory.GetFiles(rootDirectory))
            {
                result.Add(new(filename));
            }
            foreach (string directory in directories)
            {
                foreach (string filename in Directory.GetFiles(directory))
                {
                    result.Add(new(filename));
                }
            }
            return result;
        }

        /// <summary>
        /// Build a FS image for RRF
        /// </summary>
        /// <param name="fsDirectory">Root directory of files and directories to embed</param>
        /// <returns>Binary image stream</returns>
        public static MemoryStream BuildImage(string fsDirectory)
        {
            // Retrieve all files and directories
            List<string> directories = GetDirectories(fsDirectory);
            List<FileEntry> files = GetFileEntries(fsDirectory, directories);

            // Write the image
            MemoryStream stream = new();
            using (BinaryWriter writer = new(stream, Encoding.UTF8, true))
            {
                // Write header
                writer.Write(Magic);
                writer.Write((uint)0);              // dummy for the directory offset
                writer.Write((uint)files.Count);

                // Write file entry dummies
                uint filesPosition = writer.GetPosition();
                for (int i = 0; i < files.Count; i++)
                {
                    writer.Write(FileEntry.Empty);
                }

                // Rewrite directory offset
                uint directoryPosition = writer.GetPosition();
                writer.Seek(sizeof(uint), SeekOrigin.Begin);
                writer.Write(directoryPosition);
                writer.Seek(0, SeekOrigin.End);

                // Write directories
                foreach (string directory in directories)
                {
                    Console.WriteLine("Embedding directory {0}", directory);
                    writer.Write(ConvertFilename(directory, fsDirectory));
                    writer.Write('\0');
                }
                writer.Write('\0');

                // Write filenames
                foreach (FileEntry file in files)
                {
                    file.NameOffset = writer.GetPosition();
                    writer.Write(ConvertFilename(file.Filename, fsDirectory));
                    writer.Write('\0');
                }
                writer.WritePadding();

                // Write file contents
                foreach (FileEntry file in files)
                {
                    writer.Flush();
                    file.ContentOffset = (uint)stream.Position;
                    using (Stream fs = GetFileStream(file.Filename))
                    {
                        Console.WriteLine("Embedding file {0} ({1} bytes @ {2})", file.Filename, fs.Length, file.ContentOffset);
                        file.ContentLength = (uint)fs.Length;
                        fs.CopyTo(stream);
                    }
                    writer.Write('\0');
                    writer.WritePadding();
                }

                // Write actual file table
                writer.Seek((int)filesPosition, SeekOrigin.Begin);
                foreach (FileEntry file in files)
                {
                    writer.Write(file.NameOffset);
                    writer.Write(file.ContentOffset);
                    writer.Write(file.ContentLength);
                }
            }
            return stream;
        }

        private static Stream GetFileStream(string filename)
        {
            // Strip comments and empty lines automatically from G-code files
            if (filename.EndsWith(".g"))
            {
                MemoryStream strippedFile = new();
                using (StreamWriter writer = new(strippedFile, leaveOpen: true))
                {
                    using FileStream fs = new(filename, FileMode.Open, FileAccess.Read);
                    using StreamReader reader = new(fs);

                    while (true)
                    {
                        string line = reader.ReadLine();
                        if (line == null)
                        {
                            break;
                        }

                        for (int i = line.Length - 1; i >= 0; i--)
                        {
                            // Stop when a double-quote is seen
                            if (line[i] == '"')
                            {
                                break;
                            }

                            if (line[i] == ';')
                            {
                                // Strip everything after the semicolon
                                line = line[..i].TrimEnd();
                                break;
                            }
                        }

                        // Write the line only if it isn't empty
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            writer.WriteLine(line);
                        }
                    }
                }
                strippedFile.Seek(0, SeekOrigin.Begin);
                return strippedFile;
            }

            // Return just a direct reference to the file
            return new FileStream(filename, FileMode.Open, FileAccess.Read);
        }

        /// <summary>
        /// Extension method to get the current position
        /// </summary>
        /// <param name="writer">Binary writer</param>
        /// <returns>Current position in the stream</returns>
        private static uint GetPosition(this BinaryWriter writer)
        {
            writer.Flush();
            return (uint)writer.BaseStream.Position;
        }

        /// <summary>
        /// Extension method to fill up possible gaps in order to remain on a 4-byte boundary
        /// </summary>
        /// <param name="writer">Binary writer</param>
        private static void WritePadding(this BinaryWriter writer)
        {
            int bytesToWrite = 4 - (int)writer.BaseStream.Position % 4;
            if (bytesToWrite != 4)
            {
                for (int i = 0; i < bytesToWrite; i++)
                {
                    writer.Write((byte)0);
                }
            }
        }
    }
}
