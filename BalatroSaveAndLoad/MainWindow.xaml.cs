using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.IO;
using System.IO.Compression;
using System.Windows.Threading;

namespace BalatroSaveAndLoad
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private string directoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BalatroSaveAndLoad");
        private DispatcherTimer timer = new DispatcherTimer();

        public MainWindow()
        {
            InitializeComponent();

            Directory.CreateDirectory(directoryPath); // Ensure the directory exists

            timer.Tick += Timer_Tick;

            LoadList();

            for (int i = 1; i <= 10; i++)
            {
                ProfileComboBox.Items.Add(String.Format("Profile {0}", i));
                MinuteComboBox.Items.Add(String.Format("{0} minutes", i));
            }
            ProfileComboBox.SelectedIndex = 0;
            MinuteComboBox.SelectedIndex = 0;
        }

        void LoadList()
        {
            var files = Directory.GetFileSystemEntries(directoryPath, "*.jkr");
            var fileNames = files.Select(file => Path.GetFileName(file)).OrderByDescending(file => file);
            FileListBox.ItemsSource = fileNames;
        }

        string GetCurrentSaveFile()
        {
            var profileNumber = ProfileComboBox.SelectedIndex + 1;
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Balatro", profileNumber.ToString(), "save.jkr");
        }

        void Save()
        {           
            var deckNameKey = "[\"BACK\"]={[\"name\"]=\"";
            var roundKey = "[\"round\"]=";
            var saveFile = GetCurrentSaveFile();
            var profileNumber = ProfileComboBox.SelectedIndex + 1;
            try
            {
                using (FileStream compressedStream = new FileStream(saveFile, FileMode.Open, FileAccess.Read))
                using (MemoryStream outputStream = new MemoryStream())
                using (DeflateStream deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress))
                {
                    deflateStream.CopyTo(outputStream);
                    byte[] decompressedBytes = outputStream.ToArray();
                    var result = Encoding.UTF8.GetString(decompressedBytes);
                    var deckNameStart = result.IndexOf(deckNameKey) + deckNameKey.Length;
                    var deckNameEnd = result.IndexOf("\"", deckNameStart);
                    var deckName = result.Substring(deckNameStart, deckNameEnd - deckNameStart);
                    var roundStart = result.IndexOf(roundKey) + roundKey.Length;
                    var roundEnd = result.IndexOf(",", roundStart);
                    var round = result.Substring(roundStart, roundEnd - roundStart);
                    var time = File.GetLastWriteTime(saveFile);

                    var fileName = String.Format("P{0} {1:yyyy-MM-dd HH-mm-ss} {2} Round {3}.jkr", profileNumber, time, deckName, round);
                    string filePath = Path.Combine(directoryPath, fileName);

                    if (!File.Exists(filePath))
                    {
                        File.Copy(saveFile, filePath, false);
                        LoadList();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}");
            }
        }

        void Load()
        {
            try
            {
                var saveFile = GetCurrentSaveFile();
                var selectedItem = FileListBox.SelectedItem.ToString();
                var filePath = Path.Combine(directoryPath, selectedItem);
                File.Copy(filePath, saveFile, true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}");
            }
        }

        private void Save_Button_Click(object sender, RoutedEventArgs e)
        {
            Save();
        }

        private void Load_Button_Click(object sender, RoutedEventArgs e)
        {
            Load();
        }

        private void FileListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadButton.IsEnabled = FileListBox.Items.Count > 0;
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            UpdateAutoSave();
        }

        void UpdateAutoSave()
        {
            if (AutoCheckBox.IsChecked == true)
            {
                timer.Interval = TimeSpan.FromMinutes(MinuteComboBox.SelectedIndex + 1);
                timer.Start();
            }
            else
            {
                timer.Stop();
            }
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            Save();
        }

        private void MinuteComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateAutoSave();
        }
    }
}

private void OpenSavesFolder_Click(object sender, RoutedEventArgs e)
{
    Process.Start("explorer.exe", directoryPath);
}

private void RemoveSave_Click(object sender, RoutedEventArgs e)
{
    if (FileListBox.SelectedItem != null)
    {
        string selectedFile = FileListBox.SelectedItem.ToString();
        string filePath = Path.Combine(directoryPath, selectedFile);

        MessageBoxResult result = MessageBox.Show($"Are you sure you want to delete {selectedFile}?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                File.Delete(filePath);
                LoadList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while deleting the file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

// Modify the LoadList method to clear the selection after reloading:
void LoadList()
{
    var files = Directory.GetFileSystemEntries(directoryPath, "*.jkr");
    var fileNames = files.Select(file => Path.GetFileName(file)).OrderByDescending(file => file);
    FileListBox.ItemsSource = fileNames;
    FileListBox.SelectedIndex = -1; // Clear selection
}