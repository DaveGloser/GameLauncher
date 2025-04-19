using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using K4os.Compression.LZ4.Streams;

namespace GameLauncher;

public class PakFileEntry
    {
        public string FilePath { get; set; }
        public long Offset { get; set; }
        public int CompressedSize { get; set; }
        public int UncompressedSize { get; set; }
        public byte[] Checksum { get; set; }
    }

    public class PakExtractor
    {
        public void ExtractPak(string pakFilePath, string outputDirectory)
        {
            using (var pakStream = new FileStream(pakFilePath, FileMode.Open))
            using (var br = new BinaryReader(pakStream, System.Text.Encoding.UTF8, true))
            {
                char[] identifier = br.ReadChars(3);
                if (new string(identifier) != "PAK")
                    throw new Exception("Invalid PAK file.");

                int version = br.ReadInt32();
                long directoryOffset = br.ReadInt64();

                pakStream.Seek(directoryOffset, SeekOrigin.Begin);
                int fileCount = br.ReadInt32();
                var fileEntries = new List<PakFileEntry>();

                for (int i = 0; i < fileCount; i++)
                {
                    var entry = new PakFileEntry();
                    entry.FilePath = br.ReadString();
                    entry.Offset = br.ReadInt64();
                    entry.CompressedSize = br.ReadInt32();
                    entry.UncompressedSize = br.ReadInt32();
                    entry.Checksum = br.ReadBytes(16); // MD5 má 16 bajtů
                    fileEntries.Add(entry);
                }

                foreach (var entry in fileEntries)
                {
                    pakStream.Seek(entry.Offset, SeekOrigin.Begin);
                    byte[] compressedData = br.ReadBytes(entry.CompressedSize);

                    byte[] uncompressedData = new byte[entry.UncompressedSize];
                    using (var ms = new MemoryStream(compressedData))
                    using (var lz4Stream = LZ4Stream.Decode(ms))
                    {
                        lz4Stream.Read(uncompressedData, 0, entry.UncompressedSize);
                    }

                    byte[] checksum = CalculateMD5(uncompressedData);
                    if (!checksum.SequenceEqual(entry.Checksum))
                        throw new Exception($"Checksum mismatch for file {entry.FilePath}");

                    string outputPath = Path.Combine(outputDirectory, entry.FilePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                    File.WriteAllBytes(outputPath, uncompressedData);
                }
            }
        }

        private byte[] CalculateMD5(byte[] data)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                return md5.ComputeHash(data);
            }
        }
    }