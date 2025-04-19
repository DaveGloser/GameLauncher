﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GameLauncher;

public class ChunkReassembler
    {
        public void ReassembleChunksToFolder(string chunkFolder, string gameFolder, IProgress<double> progress)
        {
            var chunkFiles = Directory.GetFiles(chunkFolder, "*.pak", SearchOption.TopDirectoryOnly)
                .OrderBy(f => f)
                .ToList();

            int totalChunks = chunkFiles.Count;
            int chunksProcessed = 0;

            foreach (var chunkFileGroup in GroupChunksByFile(chunkFiles))
            {
                ReassembleFileFromChunks(chunkFileGroup.Key, chunkFileGroup.Value, gameFolder);

                chunksProcessed++;

                double percentage = 1 + ((double)chunksProcessed / totalChunks * 100);
                progress?.Report(percentage);
            }
        }
        private Dictionary<string, List<string>> GroupChunksByFile(List<string> chunkFiles)
        {
            var chunkGroups = new Dictionary<string, List<string>>();

            foreach (var chunkFile in chunkFiles)
            {
                var originalFileName = ExtractOriginalFileName(chunkFile);

                if (!chunkGroups.ContainsKey(originalFileName))
                {
                    chunkGroups[originalFileName] = new List<string>();
                }

                chunkGroups[originalFileName].Add(chunkFile);
            }

            return chunkGroups;
        }

        private string ExtractOriginalFileName(string chunkFile)
        {
            var fileName = Path.GetFileNameWithoutExtension(chunkFile);
            var partIndex = fileName.LastIndexOf("_part", StringComparison.OrdinalIgnoreCase);
            if (partIndex > 0)
            {
                return fileName.Substring(0, partIndex).Replace('_', Path.DirectorySeparatorChar);
            }
            return fileName;
        }

        private void ReassembleFileFromChunks(string originalFilePath, List<string> chunkFiles, string gameFolder)
        {
            string outputFilePath = Path.Combine(gameFolder, originalFilePath);

            string outputDirectory = Path.GetDirectoryName(outputFilePath);
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            using (var outputStream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write))
            {
                foreach (var chunkFile in chunkFiles.OrderBy(f => f))
                {
                    using (var chunkStream = new FileStream(chunkFile, FileMode.Open, FileAccess.Read))
                    {
                        chunkStream.CopyTo(outputStream);
                    }
                }
            }
        }
    }