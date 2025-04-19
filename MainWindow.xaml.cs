 using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
 using System.Net.Http;
using Newtonsoft.Json;
using System.Windows.Controls;


namespace GameLauncher

{

    public partial class MainWindow : Window
    {
        private string updaterHashUrl = @"https://game.com/HashUpdater.json";
        private string updaterPathFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "updaterPath.json");
        
       private string launcherHashFile = @"https://game.com/HashLauncher.json";
        private string launcherPathFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "launcherPath.json");

        private string clientUrl = @"https://game.com/client/";
        private string hashClientUrl = @"https://game.com/HashClient.json";
        private string hashClientPakUrl = @"https://game.com/HashClientPak.json";

        private string gameFolderPath;
        private readonly string gamePathFile;

        public MainWindow()
        {
            InitializeComponent();

            this.Loaded += MainWindow_Loaded;
            
            gamePathFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gamePath.json");
            gameFolderPath = LoadGameFolderPath();
            if (!string.IsNullOrEmpty(gameFolderPath))
            {
                CheckForUpdatesOnStartup();
            }
            else
            {
                actionButton.Content = "Install";
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            CreateOrUpdateUpdaterPath();

            CheckForUpdaterUpdate();

            CreateOrUpdateLauncherPath();

            CheckForLauncherUpdate();
            
        }
        
        private async void CheckForUpdaterUpdate()
        {
            bool updateAvailable = await IsUpdateAvailableForUpdater();

            if (updateAvailable)
            {
                var checkForUpdaterWindow = new CheckForUpdater();
                checkForUpdaterWindow.ShowDialog();
            }
        }
        
        private async Task<bool> IsUpdateAvailableForUpdater()
        {
            try
            {
                var serverHashes = await DownloadHashFile(updaterHashUrl);

                var updaterPathData = LoadUpdaterPathFile();
                if (updaterPathData == null)
                {
                    MessageBox.Show("updaterPath.json not found. Unable to check for update.");
                    return false;
                }

                foreach (KeyValuePair<string, string> serverHash in serverHashes)
                {
                    if (!updaterPathData.FileHashes.ContainsKey(serverHash.Key) ||
                        updaterPathData.FileHashes[serverHash.Key] != serverHash.Value)
                    {
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error checking updater update: {ex.Message}");
                return false;
            }
        }



        private async Task<Dictionary<string, string>> DownloadHashFile(string url)
        {
            using (HttpClient client = new HttpClient())
            {
                string json = await client.GetStringAsync(url);
                return JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            }
        }
        
        public class HashFile
        {
            public string FileName { get; set; }
            public string Hash { get; set; }
        }
        
        private LauncherPath LoadUpdaterPathFile()
        {
            if (File.Exists(updaterPathFile))
            {
                string json = File.ReadAllText(updaterPathFile);
                return JsonConvert.DeserializeObject<LauncherPath>(json);
            }
            return null;
        }
        
        private void CreateOrUpdateLauncherPath()
        {
            try
            {
                string directoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory);

                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                var fileHashes = FileHashManager.CalculateHashesFolder(directoryPath);

                var launcherPathData = new LauncherPath
                {
                    Path = directoryPath,
                    FileHashes = fileHashes
                };

                string json = JsonConvert.SerializeObject(launcherPathData, Formatting.Indented);
                File.WriteAllText(launcherPathFile, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error while creating launcherPath.json: {ex.Message}");
            }
        }
        
        private void CheckForLauncherUpdate()
        {
            if (!File.Exists(launcherPathFile))
            {
                CreateOrUpdateLauncherPath();
            }

            if (IsUpdateAvailable(launcherHashFile))
            {
                var updateWindow = new LauncherUpdater();
                updateWindow.ShowDialog();
            }
        }



        private bool IsUpdateAvailable(string serverHashFile)
        {
            try
            {
                var serverHashes = FileHashManager.LoadHashesFromFile(serverHashFile);

                var launcherPathData = LoadLauncherPathFile();
                if (launcherPathData == null)
                {
                    MessageBox.Show("launcherPath.json not found. Unable to check for update.");
                    return false;
                }

                foreach (var serverHash in serverHashes)
                {
                    if (!launcherPathData.FileHashes.ContainsKey(serverHash.Key) || launcherPathData.FileHashes[serverHash.Key] != serverHash.Value)
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error checking for update: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public class LauncherPath
        {
            public string Path { get; set; }
            public Dictionary<string, string> FileHashes { get; set; }
        }
        
        private LauncherPath LoadLauncherPathFile()
        {
            if (File.Exists(launcherPathFile))
            {
                string json = File.ReadAllText(launcherPathFile);
                return JsonConvert.DeserializeObject<LauncherPath>(json);
            }
            return null;
        }


        private string LoadGameFolderPath()
        {
            if (File.Exists(gamePathFile))
            {
                try
                {
                    string json = File.ReadAllText(gamePathFile);
                    var gamePathData = JsonConvert.DeserializeObject<GamePath>(json);
                    if (gamePathData != null && gamePathData.IsInstalled && Directory.Exists(gamePathData.Path))
                    {
                        return gamePathData.Path;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading game path: {ex.Message}");
                }
            }

            return null;
        }

        private void VerifyAndUpdateGameHashes()
        {
            string gameFolder = gameFolderPath;
            string gamePathJson = gamePathFile;
            
            GamePath gamePathData = null;

            if (File.Exists(gamePathJson))
            {
                string json = File.ReadAllText(gamePathJson);
                gamePathData = JsonConvert.DeserializeObject<GamePath>(json);
            }

            if (gamePathData == null)
            {
                gamePathData = new GamePath();
            }

            var savedHashes = gamePathData.FileHashes ?? new Dictionary<string, string>();

            var currentHashes = FileHashManager.CalculateHashesFolder(gameFolder);

            bool needsUpdate = false;
            foreach (var file in currentHashes)
            {
                if (!savedHashes.ContainsKey(file.Key) || savedHashes[file.Key] != file.Value)
                {
                    savedHashes[file.Key] = file.Value;
                    needsUpdate = true;
                }
            }

            var removedFiles = savedHashes.Keys.Except(currentHashes.Keys).ToList();
            foreach (var removedFile in removedFiles)
            {
                savedHashes.Remove(removedFile);
                needsUpdate = true;
            }

            if (needsUpdate)
            {
                gamePathData.FileHashes = savedHashes;
                string updatedJson = JsonConvert.SerializeObject(gamePathData, Formatting.Indented);
                File.WriteAllText(gamePathJson, updatedJson);
                Console.WriteLine("Game hashes updated in gamePath.json");
            }
            else
            {
                Console.WriteLine("No changes detected in game files.");
            }
        }
        private void SaveGameFolderPath(string path)
        {
            var gamePathData = new GamePath 
            { 
                Path = path, 
                IsInstalled = true,
                FileHashes = FileHashManager.CalculateHashesFolder(path)
            };

            try
            {
                string directory = Path.GetDirectoryName(gamePathFile);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonConvert.SerializeObject(gamePathData, Formatting.Indented);
                File.WriteAllText(gamePathFile, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving game path: {ex.Message}");
            }
        }


        private void StartGame()
        {
            string gameExePath = Path.Combine(gameFolderPath, "Game.exe");

            if (File.Exists(gameExePath))
            {
                Process.Start(gameExePath);
            }
            else
            {
                MessageBox.Show("Game.exe not found!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ActionButton_Click(object sender, RoutedEventArgs e)
        {
            string currentAction = (string)actionButton.Content;

            if (currentAction == "Install")
            {
                DownloadAndInstallGame();
                CheckForUpdatesOnStartup();
            }
            else if (currentAction == "Play")
            {
                StartGame();
            }
            else if (currentAction == "Update")
            {
                UpdateGame();
                CheckForUpdatesOnStartup();
            }
        }
        
    public async void StartInstallation(string installPath)
    {
        gameFolderPath = installPath;
        SaveGameFolderPath(gameFolderPath);

        string chunkUrl = clientUrl;
        string hashUrl = hashClientPakUrl;

        try
        {
            if (!Directory.Exists(gameFolderPath))
            {
                Directory.CreateDirectory(gameFolderPath);
            }

            actionButton.Visibility = Visibility.Collapsed;
            progressBar.Visibility = Visibility.Visible;

            Dispatcher.Invoke(() =>
            {
                progressBar.Minimum = 0;
                progressBar.Maximum = 100;
                progressBar.Value = 0;
            });

            var progressHandler = new Progress<double>(value =>
            {
                Dispatcher.Invoke(() => { progressBar.Value = value; });
            });

            using (HttpClient client = new HttpClient())
            {
                string hashJson = await client.GetStringAsync(hashUrl);
                var serverHashData = JsonConvert.DeserializeObject<Dictionary<string, string>>(hashJson);

                var localHashes = LoadLocalHashesFromGamePath();

                int totalChunks = serverHashData.Count;
                int downloadedChunks = 0;

                foreach (var chunk in serverHashData)
                {
                    string chunkFileName = chunk.Key;
                    string expectedHash = chunk.Value;
                    string chunkFilePath = Path.Combine(gameFolderPath, chunkFileName);

                    if (localHashes.ContainsValue(expectedHash))
                    {
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
            }

            var chunkProcessor = new ChunkProcessor();
            await Task.Run(() =>
                chunkProcessor.ReassembleChunksToFolder(gameFolderPath, gameFolderPath, progressHandler));

            var currentHashes = FileHashManager.CalculateHashesFolder(gameFolderPath);

            var gamePathData = new GamePath
            {
                Path = gameFolderPath,
                IsInstalled = true,
                FileHashes = currentHashes
            };

            string json = JsonConvert.SerializeObject(gamePathData, Formatting.Indented);
            File.WriteAllText(gamePathFile, json);

            DeletePakFiles(gameFolderPath);

            actionButton.Content = "Play";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Error during installation", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Dispatcher.Invoke(() =>
            {
                progressBar.Value = 0;
                progressBar.Visibility = Visibility.Collapsed;
                actionButton.Visibility = Visibility.Visible;
            });
        }
    }

        private Dictionary<string, string> LoadLocalHashesFromGamePath()
        {
            if (File.Exists(gamePathFile))
            {
                string json = File.ReadAllText(gamePathFile);
                var gamePathData = JsonConvert.DeserializeObject<GamePath>(json);
                return gamePathData?.FileHashes ?? new Dictionary<string, string>();
            }

            return new Dictionary<string, string>();
        }

        private string GetRelativePath(string basePath, string fullPath)
        {
            Uri baseUri = new Uri(basePath);
            Uri fullUri = new Uri(fullPath);
            return baseUri.MakeRelativeUri(fullUri).ToString().Replace('/', Path.DirectorySeparatorChar);
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

        private string GetOriginalFileName(string chunkFileName)
        {
            int index = chunkFileName.IndexOf("_part");
            if (index > 0)
            {
                return chunkFileName.Substring(0, index);
            }
            return chunkFileName;
        }
        private void DownloadAndInstallGame()
        {
            string defaultInstallPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Game");

            var installPathWindow = new InstallPathWindow(defaultInstallPath, this)
            {
                Owner = this
            };
            installPathWindow.ShowDialog();
        }
        
        private async Task UpdateGame()
        {
            string chunkUrl = clientUrl; 
            string gameFolder = gameFolderPath;
            string hashUrl = hashClientPakUrl;

            try
            {
                actionButton.Visibility = Visibility.Hidden;
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

                    var localHashes = LoadLocalHashesFromGamePath();

                    int totalChunks = serverHashData.Count;
                    int downloadedChunks = 0;

                    foreach (var chunk in serverHashData)
                    {
                        string chunkFileName = chunk.Key;
                        string expectedHash = chunk.Value;
                        string chunkFilePath = Path.Combine(gameFolder, chunkFileName);

                        if (localHashes.ContainsValue(expectedHash))
                        {
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
                }

                var chunkProcessor = new ChunkProcessor();
                await Task.Run(() =>
                    chunkProcessor.ReassembleChunksToFolder(gameFolder, gameFolder, progressHandler));

                var currentHashes = await Task.Run(() => FileHashManager.CalculateHashesFolder(gameFolder));

                var gamePathData = new GamePath
                {
                    Path = gameFolder,
                    IsInstalled = true,
                    FileHashes = currentHashes
                };

                string json = JsonConvert.SerializeObject(gamePathData, Formatting.Indented);
                File.WriteAllText(gamePathFile, json);

                DeletePakFiles(gameFolder);

                actionButton.Content = "Play";
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
                    actionButton.Visibility = Visibility.Visible;
                });
            }
        }



        public static class UpdateChecker
        {
            public static List<string> CheckForUpdates(Dictionary<string, string> localHashes, Dictionary<string, string> serverHashes)
            {
                var filesToUpdate = new List<string>();

                foreach (var serverFile in serverHashes)
                {
                    if (!localHashes.ContainsKey(serverFile.Key) || localHashes[serverFile.Key] != serverFile.Value)
                    {
                        filesToUpdate.Add(serverFile.Key);
                    }
                }

                return filesToUpdate;
            }

            private static Dictionary<string, string> LoadLocalHashes()
            {
                string gamePathFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gamePath.json");
                if (File.Exists(gamePathFile))
                {
                    string json = File.ReadAllText(gamePathFile);
                    var gamePathData = JsonConvert.DeserializeObject<GamePath>(json);
                    return gamePathData?.FileHashes ?? new Dictionary<string, string>();
                }

                return new Dictionary<string, string>();
            }

        }
        private void CreateOrUpdateUpdaterPath()
        {
            try
            {
                string directoryPath = AppDomain.CurrentDomain.BaseDirectory;

                var fileHashes = FileHashManager.CalculateHashesUpdater(directoryPath);

                var updaterPathData = new LauncherPath
                {
                    Path = directoryPath,
                    FileHashes = fileHashes
                };

                string json = JsonConvert.SerializeObject(updaterPathData, Formatting.Indented);
                File.WriteAllText(updaterPathFile, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error while creating updaterPath.json: {ex.Message}");
            }
        }


      
        private void CreateOrUpdateGamePathJson()
        {
            string gameFolder = AppDomain.CurrentDomain.BaseDirectory;
            string gamePathJson = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gamePath.json");

            var fileHashes = FileHashManager.CalculateHashesFolder(gameFolder);

            var gamePathData = new GamePath
            {
                Path = gameFolder,
                FileHashes = fileHashes
            };

            string json = JsonConvert.SerializeObject(gamePathData, Formatting.Indented);
            File.WriteAllText(gamePathJson, json);
        }
       private async void CheckForUpdatesOnStartup()
        {
            string gameHashFileUrl = hashClientUrl;

            if (!string.IsNullOrEmpty(gameFolderPath) && Directory.Exists(gameFolderPath))
            {
                var gamePathData = LoadGamePathFile();
                
                if (gamePathData == null || !gamePathData.IsInstalled || !Directory.Exists(gamePathData.Path))
                {
                    actionButton.Content = "Install";
                    return;
                }

                var localHashes = FileHashManager.CalculateHashesFolder(gamePathData.Path);

                UpdateGamePathFile(localHashes);

                var serverHashes = await LoadServerHashesFromUrl(gameHashFileUrl);
                if (serverHashes == null)
                {
                    MessageBox.Show("Failed to retrieve server hashes.");
                    return;
                }

                var filesToUpdate = UpdateChecker.CheckForUpdates(localHashes, serverHashes);

                if (filesToUpdate.Count > 0)
                {
                    actionButton.Content = "Update";
                }
                else
                {
                    actionButton.Content = "Play";
                }
            }
            else
            {
                actionButton.Content = "Install";
            }
        }
        private GamePath LoadGamePathFile()
        {
            if (File.Exists(gamePathFile))
            {
                string json = File.ReadAllText(gamePathFile);
                return JsonConvert.DeserializeObject<GamePath>(json);
            }
            return null;
        }

        private void UpdateGamePathFile(Dictionary<string, string> fileHashes)
        {
            var gamePathData = new GamePath
            {
                Path = gameFolderPath,
                IsInstalled = true,
                FileHashes = fileHashes
            };

            string json = JsonConvert.SerializeObject(gamePathData, Formatting.Indented);
            File.WriteAllText(gamePathFile, json);
        }
        
        private async Task<Dictionary<string, string>> LoadServerHashesFromUrl(string url)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    string hashJson = await client.GetStringAsync(url);

                    return JsonConvert.DeserializeObject<Dictionary<string, string>>(hashJson);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error downloading server hashes: {ex.Message}");
                return null;
            }
        }

        private bool DirectoryHasFiles(string path)
        {
            return Directory.EnumerateFiles(path).Any();
        }

        private bool AreAllFilesPresent(Dictionary<string, string> localHashes)
        {
            foreach (var file in localHashes.Keys)
            {
                string filePath = Path.Combine(gameFolderPath, file);
                if (!File.Exists(filePath))
                {
                    return false;
                }
            }
            return true;
        }
        public class GamePath
        {
            public string Path { get; set; }
            public bool IsInstalled { get; set; }
            public Dictionary<string, string> FileHashes { get; set; }
        }
        
        private void header_Loaded(object sender, RoutedEventArgs e)
        {
            InitHeader(sender as Grid);
        }

        private void InitHeader(Grid header)
        {
            var restoreIfMove = false;

            header.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2)
                {
                    if ((ResizeMode == ResizeMode.CanResize) ||
                        (ResizeMode == ResizeMode.CanResizeWithGrip))
                    {
                        SwitchState();
                    }
                }
                else
                {
                    if (WindowState == WindowState.Maximized)
                    {
                        restoreIfMove = true;
                    }

                    DragMove();
                }
            };
            header.MouseLeftButtonUp += (s, e) => { restoreIfMove = false; };
            header.MouseMove += (s, e) =>
            {
                if (restoreIfMove)
                {
                    restoreIfMove = false;
                    var mouseX = e.GetPosition(this).X;
                    var width = RestoreBounds.Width;
                    var x = mouseX - width / 2;

                    if (x < 0)
                    {
                        x = 0;
                    }
                    else if (x + width > SystemParameters.PrimaryScreenWidth)
                    {
                        x = SystemParameters.PrimaryScreenWidth - width;
                    }

                    WindowState = WindowState.Normal;
                    Left = x;
                    Top = 0;
                    DragMove();
                }
            };
        }

        private void SwitchState()
        {
            switch (WindowState)
            {
                case WindowState.Normal:
                {
                    WindowState = WindowState.Maximized;
                    break;
                }
                case WindowState.Maximized:
                {
                    WindowState = WindowState.Normal;
                    break;
                }
            }
        }

        private void TitleBar(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                this.DragMove();
        }

        private void Minimaze(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }
        
        private void Close(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Game(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://game.com/") { UseShellExecute = true });
        }

        private void Discord(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://discord.gg/cZZWkMDDd3") { UseShellExecute = true });
        }

        private void Youtube(object sender, RoutedEventArgs e)
        {
            Process.Start(
                new ProcessStartInfo("https://www.youtube.com") { UseShellExecute = true });
        }

        private void Facebook(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://www.facebook.com")
                { UseShellExecute = true });
        }

        private void Twitter(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://x.com") { UseShellExecute = true });
        }

        private void Instagram(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://www.instagram.com")
                { UseShellExecute = true });
        }

        private void TikTok(object sender, RoutedEventArgs e)
        {
            Process.Start(
                new ProcessStartInfo("https://www.tiktok.com") { UseShellExecute = true });
        }

        private void Twitch(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://www.twitch.tv") { UseShellExecute = true });
        }
    }
}