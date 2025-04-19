using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Newtonsoft.Json;


public static class FileHashManager
{
    public static Dictionary<string, string> CalculateHashesFolder(string gameFolder)
    {
        var fileHashes = new Dictionary<string, string>();
        var files = Directory.GetFiles(gameFolder, "*.*", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            string relativePath = Path.GetRelativePath(gameFolder, file);
            string fileHash = GetFileHash(file);
            fileHashes[relativePath] = fileHash;
        }

        return fileHashes;
    }
    
    public static Dictionary<string, string> CalculateHashesUpdater(string folderPath)
    {
        var allowedFiles = new HashSet<string>
        {
            "GameUpdater.deps.json",
            "GameUpdater.dll",
            "GameUpdater.exe",
            "GameUpdater.pdb",
            "GameUpdater.runtimeconfig.json"
        };

        var fileHashes = new Dictionary<string, string>();
        var files = Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly);

        foreach (var file in files)
        {
            string fileName = Path.GetFileName(file);
            if (allowedFiles.Contains(fileName))
            {
                string fileHash = GetFileHash(file);
                fileHashes[fileName] = fileHash; 
            }
        }

        return fileHashes;
    }

    public static string GetFileHash(string filePath)
    {
        using (var sha256 = System.Security.Cryptography.SHA256.Create())
        {
            using (var fileStream = File.OpenRead(filePath))
            {
                byte[] hashBytes = sha256.ComputeHash(fileStream);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
        }
    }

    public static void SaveHashesToFile(string filePath, Dictionary<string, string> fileHashes)
    {
        string json = JsonConvert.SerializeObject(fileHashes, Formatting.Indented);
        File.WriteAllText(filePath, json); 
    }
    
    public static Dictionary<string, string> LoadHashesFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return new Dictionary<string, string>();
        }

        string json = File.ReadAllText(filePath);
        return JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
    }


}