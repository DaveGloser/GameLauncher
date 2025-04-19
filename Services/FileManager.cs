using System;
using System.IO;
using System.Security.Cryptography;
using Newtonsoft.Json;


namespace GameLauncher;

public static class FileManager
{
    private static string settingsFilePath = @"C:\Path\To\YourApp\settings.json";

    public class GameSettings
    {
        public string GameFolderPath { get; set; }
    }

    public static void SaveGameFolderPath(string folderPath)
    {
        var settings = new GameSettings { GameFolderPath = folderPath };
        string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
        File.WriteAllText(settingsFilePath, json);
    }

    public static string LoadGameFolderPath()
    {
        if (File.Exists(settingsFilePath))
        {
            string json = File.ReadAllText(settingsFilePath);
            var settings = JsonConvert.DeserializeObject<GameSettings>(json);
            return settings?.GameFolderPath;
        }
        return null;
    }
}