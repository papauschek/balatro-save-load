using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
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
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private string _status = "Ready";
        public string Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged();
                }
            }
        }
        
        private string _countdownText = "";
        public string CountdownText
        {
            get => _countdownText;
            set
            {
                if (_countdownText != value)
                {
                    _countdownText = value;
                    OnPropertyChanged();
                }
            }
        }
        
        private Visibility _countdownVisibility = Visibility.Collapsed;
        public Visibility CountdownVisibility
        {
            get => _countdownVisibility;
            set
            {
                if (_countdownVisibility != value)
                {
                    _countdownVisibility = value;
                    OnPropertyChanged();
                }
            }
        }

        private string directoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BalatroSaveAndLoad");
        private DispatcherTimer timer = new DispatcherTimer();
        private DispatcherTimer statusResetTimer = new DispatcherTimer();
        private DispatcherTimer errorCheckTimer = new DispatcherTimer();
        private DispatcherTimer countdownTimer = new DispatcherTimer();
        
        // Track current error state
        private bool hasActiveError = false;
        private string currentErrorMessage = string.Empty;
        private Func<bool>? errorConditionChecker = null;
        
        // For countdown timer
        private DateTime nextAutoSaveTime;
        private double autoSaveIntervalMinutes;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            Directory.CreateDirectory(directoryPath); // Ensure the directory exists

            timer.Tick += Timer_Tick;

            // Configure status reset timer
            statusResetTimer.Interval = TimeSpan.FromSeconds(5);
            statusResetTimer.Tick += StatusResetTimer_Tick;
            
            // Configure error check timer
            errorCheckTimer.Interval = TimeSpan.FromSeconds(2);
            errorCheckTimer.Tick += ErrorCheckTimer_Tick;
            errorCheckTimer.Start(); // Start the error check timer
            
            // Configure countdown timer
            countdownTimer.Interval = TimeSpan.FromSeconds(1);
            countdownTimer.Tick += CountdownTimer_Tick;

            LoadList();

            for (int i = 1; i <= 10; i++)
            {
                ProfileComboBox.Items.Add(String.Format("Profile {0}", i));
                MinuteComboBox.Items.Add(String.Format("{0} minutes", i));
            }
            ProfileComboBox.SelectedIndex = 0;
            MinuteComboBox.SelectedIndex = 0;

            // Enable multiple selection for FileListBox
            FileListBox.SelectionMode = SelectionMode.Extended;

            UpdateStatusDisplay(false);
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void UpdateStatusDisplay(bool isError)
        {
            if (isError)
            {
                MainStatusBar.Background = Brushes.DarkRed;
                MainStatusBar.Foreground = Brushes.White;
            }
            else
            {
                MainStatusBar.Background = Brushes.LightGray;
                MainStatusBar.Foreground = Brushes.Black;
            }
        }

        private void SetErrorStatus(string errorMessage, Func<bool>? conditionChecker = null)
        {
            // Set the error state
            hasActiveError = true;
            currentErrorMessage = errorMessage;
            errorConditionChecker = conditionChecker;
            
            // Update UI
            Status = $"Error: {errorMessage}";
            UpdateStatusDisplay(true);
            FlashWindow();

            // Reset status after a delay only if no condition checker is provided
            if (errorConditionChecker == null)
            {
                statusResetTimer.Stop();
                statusResetTimer.Start();
            }
        }

        private void SetSuccessStatus(string message)
        {
            // Clear any error state
            hasActiveError = false;
            currentErrorMessage = string.Empty;
            errorConditionChecker = null;
            
            // Update UI
            Status = message;
            UpdateStatusDisplay(false);

            // Reset status after a delay
            statusResetTimer.Stop();
            statusResetTimer.Start();
        }

        private void StatusResetTimer_Tick(object? sender, EventArgs e)
        {
            statusResetTimer.Stop();
            
            // Only reset to "Ready" if there's no active error
            if (!hasActiveError)
            {
                Status = "Ready";
                UpdateStatusDisplay(false);
            }
        }
        
        private void ErrorCheckTimer_Tick(object? sender, EventArgs e)
        {
            // Check if there's an active error with a condition checker
            if (hasActiveError && errorConditionChecker != null)
            {
                try
                {
                    // Check if the error condition is resolved
                    bool isResolved = errorConditionChecker();
                    if (isResolved)
                    {
                        // Error is resolved, clear error state
                        hasActiveError = false;
                        currentErrorMessage = string.Empty;
                        errorConditionChecker = null;
                        Status = "Ready";
                        UpdateStatusDisplay(false);
                    }
                }
                catch
                {
                    // If checking causes an exception, keep the error state
                }
            }
        }
        
        private void CountdownTimer_Tick(object? sender, EventArgs e)
        {
            if (AutoCheckBox.IsChecked == true)
            {
                TimeSpan timeLeft = nextAutoSaveTime - DateTime.Now;
                if (timeLeft.TotalSeconds <= 0)
                {
                    // We've reached the time - timer event will handle the save
                    // Just update for next interval
                    UpdateNextAutoSaveTime();
                }
                else
                {
                    UpdateCountdownDisplay(timeLeft);
                }
            }
        }
        
        private void UpdateCountdownDisplay(TimeSpan timeLeft)
        {
            if (timeLeft.TotalHours >= 1)
            {
                CountdownText = $"Next auto-save: {timeLeft.Hours}h {timeLeft.Minutes}m {timeLeft.Seconds}s";
            }
            else if (timeLeft.TotalMinutes >= 1)
            {
                CountdownText = $"Next auto-save: {timeLeft.Minutes}m {timeLeft.Seconds}s";
            }
            else
            {
                CountdownText = $"Next auto-save: {timeLeft.Seconds}s";
            }
        }
        
        private void UpdateNextAutoSaveTime()
        {
            nextAutoSaveTime = DateTime.Now.AddMinutes(autoSaveIntervalMinutes);
        }

        private void FlashWindow()
        {
            // Flash the window in the taskbar
            FlashWindow(this);
        }

        // P/Invoke to flash the window in the taskbar
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool FlashWindow(IntPtr hwnd, bool bInvert);

        private static void FlashWindow(Window window)
        {
            // Get the window handle
            IntPtr windowHandle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
            // Flash the window in the taskbar
            FlashWindow(windowHandle, true);
        }

        void LoadList()
        {
            var files = Directory.GetFileSystemEntries(directoryPath, "*.jkr");
            var fileNames = files.Select(file => Path.GetFileName(file)).OrderByDescending(file => file);
            FileListBox.ItemsSource = fileNames;
            FileListBox.SelectedIndex = -1; // Clear selection
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
                if (!File.Exists(saveFile))
                {
                    SetErrorStatus(
                        $"Save file not found for Profile {profileNumber}", 
                        () => File.Exists(saveFile) // Condition will be true when file exists
                    );
                    return;
                }

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
                        SetSuccessStatus($"Saved {fileName}");
                    }
                    else
                    {
                        SetSuccessStatus($"File already exists: {fileName}");
                    }
                }
                
                // Reset auto-save countdown if this was an auto-save
                if (AutoCheckBox.IsChecked == true)
                {
                    UpdateNextAutoSaveTime();
                }
            }
            catch (Exception ex)
            {
                SetErrorStatus(ex.Message);
            }
        }

        void Load()
        {
            try
            {
                var saveFile = GetCurrentSaveFile();
                var selectedItem = FileListBox.SelectedItem?.ToString();

                if (string.IsNullOrEmpty(selectedItem))
                {
                    SetErrorStatus("No save file selected");
                    return;
                }

                var filePath = Path.Combine(directoryPath, selectedItem);

                // Check that the file exists first, then copy it
                if (!File.Exists(filePath))
                {
                    SetErrorStatus(
                        $"The selected file does not exist: {selectedItem}",
                        () => File.Exists(filePath) // Condition will be true when file exists
                    );
                    return;
                }

                File.Copy(filePath, saveFile, true);
                SetSuccessStatus($"Loaded {selectedItem}");
            }
            catch (Exception ex)
            {
                SetErrorStatus(ex.Message);
            }
        }

        private void Save_Button_Click(object sender, RoutedEventArgs e)
        {
            Save();
        }

        private void Load_Button_Click(object sender, RoutedEventArgs e)
        {
            if (FileListBox.SelectedItems.Count == 1)
            {
                Load();
            }
            else if (FileListBox.SelectedItems.Count == 0)
            {
                SetErrorStatus("Please select a save file to load");
            }
            else
            {
                SetErrorStatus("Please select only one save file to load");
            }
        }

        private void FileListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadButton.IsEnabled = FileListBox.SelectedItems.Count == 1;
            
            // If error was about needing to select a file, check if it's resolved
            if (hasActiveError && currentErrorMessage.Contains("select") && FileListBox.SelectedItems.Count == 1)
            {
                hasActiveError = false;
                Status = "Ready";
                UpdateStatusDisplay(false);
            }
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            UpdateAutoSave();
        }

        void UpdateAutoSave()
        {
            if (AutoCheckBox.IsChecked == true)
            {
                double minutes = GetSelectedMinutes();
                if (minutes > 0)
                {
                    autoSaveIntervalMinutes = minutes;
                    timer.Interval = TimeSpan.FromMinutes(minutes);
                    timer.Start();
                    
                    // Set up the countdown timer
                    UpdateNextAutoSaveTime();
                    countdownTimer.Start();
                    CountdownVisibility = Visibility.Visible;
                    
                    SetSuccessStatus($"Auto-save enabled: every {minutes} {(minutes == 1 ? "minute" : "minutes")}");
                }
                else
                {
                    AutoCheckBox.IsChecked = false;
                    countdownTimer.Stop();
                    CountdownVisibility = Visibility.Collapsed;
                    SetErrorStatus("Invalid time interval. Please enter a positive number.");
                }
            }
            else
            {
                timer.Stop();
                countdownTimer.Stop();
                CountdownVisibility = Visibility.Collapsed;
                SetSuccessStatus("Auto-save disabled");
            }
        }

        private double GetSelectedMinutes()
        {
            // If an item is selected from the dropdown list
            if (MinuteComboBox.SelectedIndex >= 0 && MinuteComboBox.SelectedIndex < 10)
            {
                return MinuteComboBox.SelectedIndex + 1;
            }

            // Try to parse custom input
            string input = MinuteComboBox.Text.Trim();

            // Extract number from potential format like "X minutes"
            Match match = Regex.Match(input, @"^(\d+\.?\d*)\s*(?:minutes?)?$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                if (double.TryParse(match.Groups[1].Value, out double result) && result > 0)
                {
                    return result;
                }
            }
            else if (double.TryParse(input, out double directResult) && directResult > 0)
            {
                return directResult;
            }

            return 0; // Invalid input
        }

        private void MinuteComboBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (AutoCheckBox.IsChecked == true)
            {
                UpdateAutoSave();
            }
            
            // If there was an error about invalid time interval, check if it's resolved
            if (hasActiveError && currentErrorMessage.Contains("time interval"))
            {
                double minutes = GetSelectedMinutes();
                if (minutes > 0)
                {
                    hasActiveError = false;
                    Status = "Ready";
                    UpdateStatusDisplay(false);
                }
            }
        }

        private void MinuteComboBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Allow only digits, decimal point, and backspace
            Regex regex = new Regex(@"^[0-9\.]+$");
            e.Handled = !regex.IsMatch(e.Text);
        }

        private void MinuteComboBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Force update on Enter key
                UpdateAutoSave();
                e.Handled = true;
            }
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            Save();
        }

        private void MinuteComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsInitialized && AutoCheckBox.IsChecked == true)
            {
                UpdateAutoSave();
            }
        }

        private void OpenSavesFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start("explorer.exe", directoryPath);
                SetSuccessStatus("Opened saves folder");
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Failed to open saves folder: {ex.Message}");
            }
        }

        private void RemoveSave_Click(object sender, RoutedEventArgs e)
        {
            DeleteSelectedSaves();
        }
        
        private void DeleteSelectedSaves()
        {
            var selectedItems = FileListBox.SelectedItems.Cast<string>().ToList();
            if (selectedItems.Count > 0)
            {
                string message = selectedItems.Count == 1
                    ? $"Are you sure you want to delete the selected save?"
                    : $"Are you sure you want to delete {selectedItems.Count} selected saves?";

                MessageBoxResult result = MessageBox.Show(message, "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        foreach (string selectedFile in selectedItems)
                        {
                            string filePath = Path.Combine(directoryPath, selectedFile);
                            if (File.Exists(filePath))
                            {
                                File.Delete(filePath);
                            }
                            else
                            {
                                SetErrorStatus($"File not found: {selectedFile}");
                            }
                        }
                        LoadList();
                        SetSuccessStatus($"Deleted {selectedItems.Count} save(s)");
                    }
                    catch (Exception ex)
                    {
                        SetErrorStatus($"Error deleting file(s): {ex.Message}");
                    }
                }
            }
        }

        private void FileListBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                DeleteSelectedSaves();
            }
        }
    }
}
