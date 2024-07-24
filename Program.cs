using System;
using System.IO;
using System.Reflection;

namespace CrcAppender
{
    class Program
    {
        static int Main(string[] args)
        {
            // Show help if nothing is passed
            if (args.Length == 0)
            {
                Console.WriteLine("CRC32 appender with optional FS image embedder v{0} by Duet3D Ltd", Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion);
                Console.WriteLine("Usage: {0} <FirmwareImage.bin> [<FS root directory>]", Path.GetFileName(Environment.ProcessPath));
                return 0;
            }

            // Parse command-line arguments
            string fsDirectory = null, firmwareFilename = null;
            foreach (string arg in args)
            {
                if (Directory.Exists(arg))
                {
                    if (!string.IsNullOrEmpty(fsDirectory))
                    {
                        Console.Error.WriteLine("Only a single FS directory may be specified");
                        return 1;
                    }
                    fsDirectory = Path.GetFullPath(arg);
                }
                else if (File.Exists(arg))
                {
                    if (!string.IsNullOrEmpty(fsDirectory))
                    {
                        Console.Error.WriteLine("Only a single firmware file may be specified");
                        return 1;
                    }
                    firmwareFilename = Path.GetFullPath(arg);
                }
                else
                {
                    Console.Error.WriteLine("Invalid file or directory: {0}", arg);
                    return 1;
                }
            }

            // Make sure we have a binary to work with...
            if (string.IsNullOrEmpty(firmwareFilename))
            {
                Console.Error.WriteLine("No firmware binary specified");
                return 1;
            }
            using FileStream firmwareFile = new(firmwareFilename, FileMode.Open, FileAccess.ReadWrite);

            // Make sure the binary is long enough
            if (firmwareFile.Length < Crc32Address + sizeof(uint))
            {
                Console.Error.WriteLine("Firmware binary is too small");
                return 1;
            }

            // Need an aligned binary...
            if (firmwareFile.Length % 4 != 0)
            {
                Console.Error.Write("Firmware binary is not aligned");
                return 1;
            }

            // Report what we have
            Console.WriteLine("Firmware binary: {0}", firmwareFilename);

            // Append files from the directory if applicable
            if (!string.IsNullOrEmpty(fsDirectory))
            {
                Console.WriteLine("FS root directory: {0}", fsDirectory);

                // Build the FS image and append it to the binary
                using MemoryStream fsImage = FS.BuildImage(fsDirectory);
                fsImage.Seek(0, SeekOrigin.Begin);
                firmwareFile.Seek(0, SeekOrigin.End);
                fsImage.CopyTo(firmwareFile);

                // Fix the address pointing to the end of the binary (= CRC32)
                FixCrc32Address(firmwareFile, fsImage.Length);
            }

            // Generate final CRC32 checksum and append it
            return AppendCrc32(firmwareFile) ? 0 : 1;
        }

        private const long Crc32Address = 0x1C;

        private static void FixCrc32Address(FileStream binaryStream, long fsLength)
        {
            Span<byte> crc32Address = stackalloc byte[sizeof(uint)];
            binaryStream.Seek(Crc32Address, SeekOrigin.Begin);
            binaryStream.Read(crc32Address);

            crc32Address = BitConverter.GetBytes(BitConverter.ToUInt32(crc32Address) + (uint)fsLength);

            binaryStream.Seek(Crc32Address, SeekOrigin.Begin);
            binaryStream.Write(crc32Address);
        }

        private static bool AppendCrc32(FileStream target)
        {
            byte[] content = new byte[(int)target.Length];
            target.Seek(0, SeekOrigin.Begin);
            if (target.Read(content) != target.Length)
            {
                Console.Error.Write("Failed to read entire file");
                return false;
            }

            uint crc32 = CRC32.Calculate(content);
            Console.WriteLine("CRC32 = 0x{0}", BitConverter.ToString(BitConverter.GetBytes(crc32)).Replace("-", string.Empty));
            target.Write(BitConverter.GetBytes(crc32));
            return true;
        }
    }
}
