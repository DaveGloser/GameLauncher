using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using System.Windows.Input;
using System.Linq;

namespace GameLauncher
{
    public partial class CheckForUpdater : Window
    {
        private string updaterHashUrl = "https://YourUrl.com/HashUpdater.json";
        private string updaterPakUrl = "https://YourUrl.com/HashUpdaterPak.json";
        private string updaterFilesUrl = "https://YourUrl.com/updater"; 
        private string updaterPathFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "updaterPath.json"); 
        private string localUpdaterFolder = AppDomain.CurrentDomain.BaseDirectory; 

        public CheckForUpdater()
        {
            InitializeComponent();
            
            CheckForUpdate();
        }

        public class LauncherPath
        {
            public string Path { get; set; }
            public Dictionary<string, string> FileHashes { get; set; }
        }

        private void CreateOrUpdateUpdaterPath()
        {
            try
            {
                var filesToTrack = new string[]
                {
                    "GameUpdater.deps.json",
                    "GameUpdater.dll",
                    "GameUpdater.exe",
                    "GameUpdater.pdb",
                    "GameUpdater.runtimeconfig.json"
                };

                var fileHashes = new Dictionary<string, string>();

                foreach (var file in filesToTrack)
                {
                    string filePath = Path.Combine(localUpdaterFolder, file);
                    if (File.Exists(filePath))
                    {
                        fileHashes[file] = GetFileHash(filePath);
                    }
                }

                var updaterPathData = new LauncherPath
                {
                    Path = localUpdaterFolder,
                    FileHashes = fileHashes
                };

                string json = JsonConvert.SerializeObject(updaterPathData, Formatting.Indented);
                File.WriteAllText(updaterPathFile, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error while creating/updating updaterPath.json: {ex.Message}");
            }
        }

        public async Task<Dictionary<string, string>> DownloadHashFile(string url)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    string jsonContent = await client.GetStringAsync(url);
                    return JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonContent);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error downloading hash file: {ex.Message}");
                return null;
            }
        }

        private async void CheckForUpdate()
        {
            CreateOrUpdateUpdaterPath();
            
            var serverHashes = await DownloadHashFile(updaterHashUrl);
            if (serverHashes == null)
            {
                MessageBox.Show("Failed to retrieve server hash file.");
                return;
            }

            var localUpdaterData = LoadLocalUpdaterPath();

            bool updateNeeded = false;
            foreach (var serverFile in serverHashes)
            {
                if (!localUpdaterData.FileHashes.ContainsKey(serverFile.Key) ||
                    localUpdaterData.FileHashes[serverFile.Key] != serverFile.Value)
                {
                    updateNeeded = true;
                    break;
                }
            }

            if (updateNeeded)
            {
                updateButton.Visibility = Visibility.Visible;
            }
            else
            {
                updateButton.Visibility = Visibility.Collapsed;
            }
        }

        private LauncherPath LoadLocalUpdaterPath()
        {
            if (File.Exists(updaterPathFile))
            {
                string json = File.ReadAllText(updaterPathFile);
                return JsonConvert.DeserializeObject<LauncherPath>(json);
            }
            return new LauncherPath
            {
                Path = localUpdaterFolder,
                FileHashes = new Dictionary<string, string>()
            };
        }

        private string GetFileHash(string filePath)
        {
            using (var sha256 = SHA256.Create())
            {
                using (var fileStream = File.OpenRead(filePath))
                {
                    byte[] hashBytes = sha256.ComputeHash(fileStream);
                    return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                }
            }
        }

private async void UpdateButton_Click(object sender, RoutedEventArgs e)
{
    string chunkUrl = updaterFilesUrl;
    string updaterFolder = AppDomain.CurrentDomain.BaseDirectory;
    string hashUrl = updaterPakUrl;

    try
    {
        updateButton.Visibility = Visibility.Hidden;
        progressBar.Visibility = Visibility.Visible;

        Dispatcher.Invoke(() =>
        {
            progressBar.Minimum = 0;
            progressBar.Maximum = 100;
            progressBar.Value = 0;
        });

        var progressHandler = new Progress<double>(percent =>
        {
            Dispatcher.Invoke(() => { progressBar.Value = percent; });
        });

        using (HttpClient client = new HttpClient())
        {
            string hashJson = await client.GetStringAsync(hashUrl);
            var serverHashData = JsonConvert.DeserializeObject<Dictionary<string, string>>(hashJson);

            var localHashes = LoadLocalHashesFromUpdaterPath();

            int totalChunks = serverHashData.Count;
            int downloadedChunks = 0;

            foreach (var chunk in serverHashData)
            {
                string chunkFileName = chunk.Key;
                string expectedHash = chunk.Value;
                string chunkFilePath = Path.Combine(updaterFolder, chunkFileName);

                if (localHashes.ContainsKey(chunkFileName) && localHashes[chunkFileName] == expectedHash)
                {
                    Console.WriteLine($"The file {chunkFileName} is up-to-date and will not be downloaded.");
                    downloadedChunks++;
                    continue;
                }

                string chunkUrlWithFile = $"{chunkUrl}/{chunkFileName}";
                using (HttpResponseMessage response = await client.GetAsync(chunkUrlWithFile, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    using (var fileStream = new FileStream(chunkFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await response.Content.CopyToAsync(fileStream);
                    }
                }

                downloadedChunks++;
                double progressValue = ((double)downloadedChunks / totalChunks) * 100;
                (progressHandler as IProgress<double>).Report(progressValue);
            }

            var chunkProcessor = new ChunkProcessor();
            await Task.Run(() =>
                chunkProcessor.ReassembleChunksToFolder(updaterFolder, updaterFolder, progressHandler));

            var currentHashes = await Task.Run(() => FileHashManager.CalculateHashesFolder(updaterFolder));

            var updaterPathData = new LauncherPath
            {
                Path = updaterFolder,
                FileHashes = currentHashes
            };

            string json = JsonConvert.SerializeObject(updaterPathData, Formatting.Indented);
            File.WriteAllText(updaterPathFile, json);

            DeletePakFiles(updaterFolder);
            
            this.Close();
        }
    }
    catch (HttpRequestException httpEx)
    {
        MessageBox.Show(httpEx.Message, "Error during download", MessageBoxButton.OK, MessageBoxImage.Error);
    }
    catch (Exception ex)
    {
        MessageBox.Show(ex.Message, "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }
    finally
    {
        Dispatcher.Invoke(() =>
        {
            progressBar.Value = 0;
            progressBar.Visibility = Visibility.Collapsed;
            updateButton.Visibility = Visibility.Visible;
        });
    }
}


        private void DeletePakFiles(string folderPath)
        {
            try
            {
                var pakFiles = Directory.GetFiles(folderPath, "*.pak", SearchOption.AllDirectories);
                foreach (var file in pakFiles)
                {
                    File.Delete(file);
                    Console.WriteLine($"Deleted {file}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting .pak files: {ex.Message}");
            }
        }

        private void TitleBar(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                this.DragMove();
        }

        private void Close(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private Dictionary<string, string> LoadLocalHashesFromUpdaterPath()
        {
            if (File.Exists(updaterPathFile))
            {
                string json = File.ReadAllText(updaterPathFile);
                var updaterPathData = JsonConvert.DeserializeObject<LauncherPath>(json);
                return updaterPathData?.FileHashes ?? new Dictionary<string, string>();
            }
            return new Dictionary<string, string>();
        }
    }
}
