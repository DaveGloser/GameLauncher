using System.IO;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Input;

namespace GameLauncher
{
    public partial class InstallPathWindow : Window
    {
        public string InstallPath { get; set; }
        private MainWindow mainWindow;

        public InstallPathWindow(string defaultPath, MainWindow mainWin)
        {
            InitializeComponent();
            InstallPath = defaultPath;
            InstallPathTextBox.Text = InstallPath;
            mainWindow = mainWin;
        }
        
        private void TitleBar(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                this.DragMove();
        }
        
        private void Close(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        
        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select any file inside the folder where the game will be installed",
            };

            if (dialog.ShowDialog() == true)
            {
                var selectedFolderPath = dialog.FolderName;
                InstallPath = selectedFolderPath;
                InstallPathTextBox.Text = InstallPath;
            }
        }
        
        private void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(InstallPath))
            {
                try
                {
                    Directory.CreateDirectory(InstallPath);
                }
                catch
                {
                    MessageBox.Show("The selected folder cannot be created. Please choose a different path.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            mainWindow.StartInstallation(InstallPath);
            this.Close();
        }
    }
}