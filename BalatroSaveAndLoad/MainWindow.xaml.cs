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
using System.Linq;
using System.Collections.ObjectModel;

namespace BalatroSaveAndLoad {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged {
        public event PropertyChangedEventHandler? PropertyChanged;

        private string _status = "Ready";

        public string Status {
            get => _status;
            set
                {
                    if (_status != value) {
                        _status = value;
                        OnPropertyChanged();
                    }
                }
        }

        private string _countdownText = "";

        public string CountdownText {
            get => _countdownText;
            set
                {
                    if (_countdownText != value) {
                        _countdownText = value;
                        OnPropertyChanged();
                    }
                }
        }

        private Visibility _countdownVisibility = Visibility.Collapsed;

        public Visibility CountdownVisibility {
            get => _countdownVisibility;
            set
                {
                    if (_countdownVisibility != value) {
                        _countdownVisibility = value;
                        OnPropertyChanged();
                    }
                }
        }

        private string _directoryPath = Path.Combine(
                                                     Environment.GetFolderPath(
                                                                               Environment.SpecialFolder.ApplicationData
                                                                              ),
                                                     "BalatroSaveAndLoad"
                                                    );

        private DispatcherTimer _timer = new DispatcherTimer();
        private DispatcherTimer _statusResetTimer = new DispatcherTimer();
        private DispatcherTimer _errorCheckTimer = new DispatcherTimer();
        private DispatcherTimer _countdownTimer = new DispatcherTimer();
        private DispatcherTimer _cleanupTimer = new DispatcherTimer();
        private DispatcherTimer _balatroCheckTimer = new DispatcherTimer();

        // Track current error state
        private bool _hasActiveError = false;
        private string _currentErrorMessage = string.Empty;
        private Func<bool>? _errorConditionChecker = null;

        // For countdown timer
        private DateTime _nextAutoSaveTime;
        private double _autoSaveIntervalMinutes;

        // For auto-cleanup
        private TimeSpan _cleanupTimeSpan = TimeSpan.FromDays(7); // Default to 7 days
        private readonly Dictionary<int, TimeSpan> _cleanupOptions = new Dictionary<int, TimeSpan>();

        private bool _isBalatroRunning = false;

        public bool IsBalatroRunning {
            get => _isBalatroRunning;
            set
                {
                    if (_isBalatroRunning != value) {
                        _isBalatroRunning = value;
                        OnPropertyChanged();
                        OnPropertyChanged(nameof(BalatroRunningStatus));
                        OnPropertyChanged(nameof(IsSaveEnabled));
                    }
                }
        }

        public string BalatroRunningStatus => IsBalatroRunning ? "Balatro: Running" : "Balatro: Not Running";
        public bool IsSaveEnabled => IsBalatroRunning;

        private DebugWindow? _debugWindow;
        private ObservableCollection<string> _debugLog = new ObservableCollection<string>();

        public MainWindow() {
            InitializeComponent();
            DataContext = this;

            Directory.CreateDirectory(_directoryPath); // Ensure the directory exists

            _timer.Tick += Timer_Tick;

            // Configure status reset timer
            _statusResetTimer.Interval = TimeSpan.FromSeconds(5);
            _statusResetTimer.Tick += StatusResetTimer_Tick;

            // Configure error check timer
            _errorCheckTimer.Interval = TimeSpan.FromSeconds(2);
            _errorCheckTimer.Tick += ErrorCheckTimer_Tick;
            _errorCheckTimer.Start(); // Start the error check timer

            // Configure countdown timer
            _countdownTimer.Interval = TimeSpan.FromSeconds(1);
            _countdownTimer.Tick += CountdownTimer_Tick;

            // Configure cleanup timer
            _cleanupTimer.Interval = TimeSpan.FromHours(1); // Check once per hour
            _cleanupTimer.Tick += CleanupTimer_Tick;

            // Setup Balatro running check timer
            _balatroCheckTimer.Interval = TimeSpan.FromSeconds(2);
            _balatroCheckTimer.Tick += BalatroCheckTimer_Tick;
            _balatroCheckTimer.Start();

            LoadList();

            for (int i = 1; i <= 10; i++) {
                ProfileComboBox.Items.Add(String.Format("Profile {0}", i));
                MinuteComboBox.Items.Add(String.Format("{0} minutes", i));
            }

            // Setup cleanup options
            _cleanupOptions.Add(0, TimeSpan.FromDays(1));
            _cleanupOptions.Add(1, TimeSpan.FromDays(3));
            _cleanupOptions.Add(2, TimeSpan.FromDays(7));
            _cleanupOptions.Add(3, TimeSpan.FromDays(14));
            _cleanupOptions.Add(4, TimeSpan.FromDays(30));

            CleanupTimeComboBox.Items.Add("1 day");
            CleanupTimeComboBox.Items.Add("3 days");
            CleanupTimeComboBox.Items.Add("7 days");
            CleanupTimeComboBox.Items.Add("14 days");
            CleanupTimeComboBox.Items.Add("30 days");

            ProfileComboBox.SelectedIndex = 0;
            MinuteComboBox.SelectedIndex = 0;
            CleanupTimeComboBox.SelectedIndex = 2; // Default to 7 days

            // Enable multiple selection for FileListBox
            FileListBox.SelectionMode = SelectionMode.Extended;

            UpdateStatusDisplay(false);
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void UpdateStatusDisplay(bool isError) {
            if (isError) {
                MainStatusBar.Background = Brushes.DarkRed;
                MainStatusBar.Foreground = Brushes.White;
            } else {
                MainStatusBar.Background = Brushes.White;
                MainStatusBar.Foreground = Brushes.Black;
            }
        }

        private void SetErrorStatus(string errorMessage, Func<bool>? conditionChecker = null) {
            // Set the error state
            _hasActiveError = true;
            _currentErrorMessage = errorMessage;
            _errorConditionChecker = conditionChecker;

            // Update UI
            Status = $"Error: {errorMessage}";
            UpdateStatusDisplay(true);
            FlashWindow();

            // Reset status after a delay only if no condition checker is provided
            if (_errorConditionChecker == null) {
                _statusResetTimer.Stop();
                _statusResetTimer.Start();
            }
        }

        private void SetSuccessStatus(string message) {
            // Clear any error state
            _hasActiveError = false;
            _currentErrorMessage = string.Empty;
            _errorConditionChecker = null;

            // Update UI
            Status = message;
            UpdateStatusDisplay(false);

            // Reset status after a delay
            _statusResetTimer.Stop();
            _statusResetTimer.Start();
        }

        private void StatusResetTimer_Tick(object? sender, EventArgs e) {
            _statusResetTimer.Stop();

            // Only reset to "Ready" if there's no active error
            if (!_hasActiveError) {
                Status = "Ready";
                UpdateStatusDisplay(false);
            }
        }

        private void ErrorCheckTimer_Tick(object? sender, EventArgs e) {
            // Check if there's an active error with a condition checker
            if (_hasActiveError && _errorConditionChecker != null) {
                try {
                    // Check if the error condition is resolved
                    bool isResolved = _errorConditionChecker();
                    if (isResolved) {
                        // Error is resolved, clear error state
                        _hasActiveError = false;
                        _currentErrorMessage = string.Empty;
                        _errorConditionChecker = null;
                        Status = "Ready";
                        UpdateStatusDisplay(false);
                    }
                }
                catch {
                    // If checking causes an exception, keep the error state
                }
            }
        }

        private void CountdownTimer_Tick(object? sender, EventArgs e) {
            if (AutoCheckBox.IsChecked == true) {
                TimeSpan timeLeft = _nextAutoSaveTime - DateTime.Now;
                if (timeLeft.TotalSeconds <= 0) {
                    // We've reached the time - timer event will handle the save
                    // Just update for next interval
                    UpdateNextAutoSaveTime();
                } else { UpdateCountdownDisplay(timeLeft); }
            }
        }

        private void UpdateCountdownDisplay(TimeSpan timeLeft) {
            if (timeLeft.TotalHours >= 1) {
                CountdownText = $"Next auto-save: {timeLeft.Hours}h {timeLeft.Minutes}m {timeLeft.Seconds}s";
            } else if (timeLeft.TotalMinutes >= 1) {
                CountdownText = $"Next auto-save: {timeLeft.Minutes}m {timeLeft.Seconds}s";
            } else { CountdownText = $"Next auto-save: {timeLeft.Seconds}s"; }
        }

        private void UpdateNextAutoSaveTime() { _nextAutoSaveTime = DateTime.Now.AddMinutes(_autoSaveIntervalMinutes); }

        private void FlashWindow() {
            // Flash the window in the taskbar
            FlashWindow(this);
        }

        // P/Invoke to flash the window in the taskbar
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool FlashWindow(IntPtr hwnd, bool bInvert);

        private static void FlashWindow(Window window) {
            // Get the window handle
            IntPtr windowHandle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
            // Flash the window in the taskbar
            FlashWindow(windowHandle, true);
        }

        void LoadList() {
            var files = Directory.GetFileSystemEntries(_directoryPath, "*.jkr");
            var fileNames = files.Select(file => Path.GetFileName(file)).OrderByDescending(file => file);
            FileListBox.ItemsSource = fileNames;
            FileListBox.SelectedIndex = -1; // Clear selection
        }

        string GetCurrentSaveFile() {
            var profileNumber = ProfileComboBox.SelectedIndex + 1;
            return Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                "Balatro",
                                profileNumber.ToString(),
                                "save.jkr"
                               );
        }

        void Save() {
            if (!IsBalatroRunning) {
                SetErrorStatus("Cannot save: Balatro is not running.");
                DebugLog("Save blocked: Balatro not running.");
                return;
            }

            DebugLog("Attempting to save...");
            var deckNameKey = "[\"BACK\"]={[\"name\"]=\"";
            var roundKey = "[\"round\"]=";
            var saveFile = GetCurrentSaveFile();
            var profileNumber = ProfileComboBox.SelectedIndex + 1;
            try {
                if (!File.Exists(saveFile)) {
                    SetErrorStatus(
                                   $"Save file not found for Profile {profileNumber}",
                                   () => File.Exists(saveFile) // Condition will be true when file exists
                                  );
                    DebugLog($"Save file not found for Profile {profileNumber}");
                    return;
                }

                using (FileStream compressedStream = new FileStream(saveFile, FileMode.Open, FileAccess.Read))
                    using (MemoryStream outputStream = new MemoryStream())
                        using (DeflateStream deflateStream =
                               new DeflateStream(compressedStream, CompressionMode.Decompress)) {
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

                            var fileName = String.Format(
                                                         "P{0} {1:yyyy-MM-dd HH-mm-ss} {2} Round {3}.jkr",
                                                         profileNumber,
                                                         time,
                                                         deckName,
                                                         round
                                                        );
                            string filePath = Path.Combine(_directoryPath, fileName);

                            if (!File.Exists(filePath)) {
                                File.Copy(saveFile, filePath, false);
                                LoadList();
                                SetSuccessStatus($"Saved {fileName}");
                                DebugLog($"Saved {fileName}");
                            } else {
                                SetSuccessStatus($"File already exists: {fileName}");
                                DebugLog($"File already exists: {fileName}");
                            }
                        }

                // Reset auto-save countdown if this was an auto-save
                if (AutoCheckBox.IsChecked == true) { UpdateNextAutoSaveTime(); }
            }
            catch (Exception ex) {
                SetErrorStatus(ex.Message);
                DebugLog($"Error during save: {ex.Message}");
            }

            DebugLog("Save completed.");
        }

        void Load() {
            DebugLog("Attempting to load...");
            try {
                var saveFile = GetCurrentSaveFile();
                var selectedItem = FileListBox.SelectedItem?.ToString();

                if (string.IsNullOrEmpty(selectedItem)) {
                    SetErrorStatus("No save file selected");
                    DebugLog("Load failed: No save file selected.");
                    return;
                }

                var filePath = Path.Combine(_directoryPath, selectedItem);

                // Check that the file exists first, then copy it
                if (!File.Exists(filePath)) {
                    SetErrorStatus(
                                   $"The selected file does not exist: {selectedItem}",
                                   () => File.Exists(filePath) // Condition will be true when file exists
                                  );
                    DebugLog($"Load failed: File does not exist: {selectedItem}");
                    return;
                }

                File.Copy(filePath, saveFile, true);
                SetSuccessStatus($"Loaded {selectedItem}");
                DebugLog($"Loaded {selectedItem}");
            }
            catch (Exception ex) {
                SetErrorStatus(ex.Message);
                DebugLog($"Error during load: {ex.Message}");
            }

            DebugLog("Load completed.");
        }

        private void Save_Button_Click(object sender, RoutedEventArgs e) {
            if (IsBalatroRunning) Save();
        }

        private void Load_Button_Click(object sender, RoutedEventArgs e) {
            if (FileListBox.SelectedItems.Count == 1) { Load(); } else if (FileListBox.SelectedItems.Count == 0) {
                SetErrorStatus("Please select a save file to load");
                DebugLog("Load failed: No save file selected.");
            } else {
                SetErrorStatus("Please select only one save file to load");
                DebugLog("Load failed: Multiple save files selected.");
            }
        }

        private void FileListBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            LoadButton.IsEnabled = FileListBox.SelectedItems.Count == 1;

            // If error was about needing to select a file, check if it's resolved
            if (_hasActiveError &&
                _currentErrorMessage.Contains("select") &&
                FileListBox.SelectedItems.Count == 1) {
                _hasActiveError = false;
                Status = "Ready";
                UpdateStatusDisplay(false);
            }
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e) { UpdateAutoSave(); }

        void UpdateAutoSave() {
            if (AutoCheckBox.IsChecked == true) {
                double minutes = GetSelectedMinutes();
                if (minutes > 0) {
                    _autoSaveIntervalMinutes = minutes;
                    if (IsBalatroRunning) {
                        _timer.Interval = TimeSpan.FromMinutes(minutes);
                        _timer.Start();
                        UpdateNextAutoSaveTime();
                        _countdownTimer.Start();
                        CountdownVisibility = Visibility.Visible;
                    } else {
                        _timer.Stop();
                        _countdownTimer.Stop();
                        CountdownVisibility = Visibility.Collapsed;
                    }

                    SetSuccessStatus($"Auto-save enabled: every {minutes} {(minutes == 1 ? "minute" : "minutes")}");
                    DebugLog($"Auto-save enabled: every {minutes} {(minutes == 1 ? "minute" : "minutes")}");
                } else {
                    AutoCheckBox.IsChecked = false;
                    _countdownTimer.Stop();
                    CountdownVisibility = Visibility.Collapsed;
                    SetErrorStatus("Invalid time interval. Please enter a positive number.");
                    DebugLog("Auto-save failed: Invalid time interval.");
                }
            } else {
                _timer.Stop();
                _countdownTimer.Stop();
                CountdownVisibility = Visibility.Collapsed;
                SetSuccessStatus("Auto-save disabled");
                DebugLog("Auto-save disabled.");
            }
        }

        private double GetSelectedMinutes() {
            // If an item is selected from the dropdown list
            if (MinuteComboBox.SelectedIndex >= 0 &&
                MinuteComboBox.SelectedIndex < 10) { return MinuteComboBox.SelectedIndex + 1; }

            // Try to parse custom input
            string input = MinuteComboBox.Text.Trim();

            // Extract number from potential format like "X minutes"
            Match match = Regex.Match(input, @"^(\d+\.?\d*)\s*(?:minutes?)?$", RegexOptions.IgnoreCase);
            if (match.Success) {
                if (double.TryParse(match.Groups[1].Value, out double result) &&
                    result > 0) { return result; }
            } else if (double.TryParse(input, out double directResult) &&
                       directResult > 0) { return directResult; }

            return 0; // Invalid input
        }

        private void MinuteComboBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (AutoCheckBox.IsChecked == true) { UpdateAutoSave(); }

            // If there was an error about invalid time interval, check if it's resolved
            if (_hasActiveError && _currentErrorMessage.Contains("time interval")) {
                double minutes = GetSelectedMinutes();
                if (minutes > 0) {
                    _hasActiveError = false;
                    Status = "Ready";
                    UpdateStatusDisplay(false);
                }
            }
        }

        private void MinuteComboBox_PreviewTextInput(object sender, TextCompositionEventArgs e) {
            // Allow only digits, decimal point, and backspace
            Regex regex = new Regex(@"^[0-9\.]+$");
            e.Handled = !regex.IsMatch(e.Text);
        }

        private void MinuteComboBox_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                // Force update on Enter key
                UpdateAutoSave();
                e.Handled = true;
            }
        }

        private void Timer_Tick(object? sender, EventArgs e) { Save(); }

        private void MinuteComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (IsInitialized && AutoCheckBox.IsChecked == true) { UpdateAutoSave(); }
        }

        private void BalatroCheckTimer_Tick(object? sender, EventArgs e) {
            bool running = IsBalatroProcessRunning();
            IsBalatroRunning = running;

            // If not running, stop autosave timer and hide countdown
            if (!running) {
                _timer.Stop();
                _countdownTimer.Stop();
                CountdownVisibility = Visibility.Collapsed;
            } else if (AutoCheckBox.IsChecked == true) {
                // If running and autosave enabled, ensure timer is running
                _timer.Start();
                _countdownTimer.Start();
                CountdownVisibility = Visibility.Visible;
            }
        }

        private bool IsBalatroProcessRunning() {
            try {
                // Check for process named "balatro" (case-insensitive, without extension)
                return Process.GetProcessesByName("balatro").Any();
            }
            catch { return false; }
        }

        private void OpenSavesFolder_Click(object sender, RoutedEventArgs e) {
            DebugLog("OpenSavesFolder_Click triggered.");
            try {
                Process.Start("explorer.exe", _directoryPath);
                SetSuccessStatus("Opened saves folder");
                DebugLog("Opened saves folder.");
            }
            catch (Exception ex) {
                SetErrorStatus($"Failed to open saves folder: {ex.Message}");
                DebugLog($"Failed to open saves folder: {ex.Message}");
            }
        }

        private void RemoveSave_Click(object sender, RoutedEventArgs e) {
            DebugLog("RemoveSave_Click triggered.");
            DeleteSelectedSaves();
        }

        private void DeleteSelectedSaves() {
            var selectedItems = FileListBox.SelectedItems.Cast<string>().ToList();
            if (selectedItems.Count > 0) {
                string message = selectedItems.Count == 1
                                     ? $"Are you sure you want to delete the selected save?"
                                     : $"Are you sure you want to delete {selectedItems.Count} selected saves?";

                MessageBoxResult result = MessageBox.Show(
                                                          message,
                                                          "Confirm Delete",
                                                          MessageBoxButton.YesNo,
                                                          MessageBoxImage.Warning
                                                         );

                if (result == MessageBoxResult.Yes) {
                    try {
                        foreach (string selectedFile in selectedItems) {
                            string filePath = Path.Combine(_directoryPath, selectedFile);
                            if (File.Exists(filePath)) {
                                File.Delete(filePath);
                                DebugLog($"Deleted save file: {selectedFile}");
                            } else {
                                SetErrorStatus($"File not found: {selectedFile}");
                                DebugLog($"Delete failed: File not found: {selectedFile}");
                            }
                        }

                        LoadList();
                        SetSuccessStatus($"Deleted {selectedItems.Count} save(s)");
                        DebugLog($"Deleted {selectedItems.Count} save(s)");
                    }
                    catch (Exception ex) {
                        SetErrorStatus($"Error deleting file(s): {ex.Message}");
                        DebugLog($"Error deleting file(s): {ex.Message}");
                    }
                }
            }
        }

        private void FileListBox_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Delete) { DeleteSelectedSaves(); }
        }

        private void CleanupTimer_Tick(object? sender, EventArgs e) { CleanupOldFiles(); }


        private void CleanupOldFiles() {
            try {
                DebugLog("Starting auto-cleanup check...");

                if (!AutoCleanCheckBox.IsChecked == true) {
                    DebugLog("Auto-cleanup is disabled, skipping");
                    return;
                }

                DebugLog($"Cleaning files older than {_cleanupTimeSpan.TotalDays} days");
                var files = Directory.GetFiles(_directoryPath, "*.jkr");
                DebugLog($"Found {files.Length} total save files to check");

                int cleanedCount = 0;
                int autoSaveCount = 0;

                foreach (var file in files) {
                    var fileName = Path.GetFileName(file);

                    // Check if this is an auto-save by looking for the pattern
                    bool isAutoSave = IsAutoSaveFile(fileName);

                    if (isAutoSave) {
                        autoSaveCount++;
                        var creationTime = File.GetCreationTime(file);
                        var now = DateTime.Now;
                        var fileAge = now - creationTime;

                        DebugLog($"Checking auto-save: {fileName}, Age: {fileAge.TotalDays:F1} days");

                        if (fileAge > _cleanupTimeSpan) {
                            File.Delete(file);
                            cleanedCount++;
                            DebugLog($"Auto-cleaned file: {fileName}");
                        } else { DebugLog($"Keeping file: {fileName} (not old enough)"); }
                    }
                }

                DebugLog($"Auto-cleanup summary: {autoSaveCount} auto-saves found, {cleanedCount} deleted");

                if (cleanedCount > 0) {
                    LoadList();
                    SetSuccessStatus($"Auto-cleaned {cleanedCount} old save(s)");
                    DebugLog($"Auto-cleaned {cleanedCount} old save(s)");
                } else { DebugLog("No files needed cleaning"); }
            }
            catch (Exception ex) {
                // Log error but don't display - this happens in the background
                Debug.WriteLine($"Error during auto-cleanup: {ex.Message}");
                DebugLog($"Error during auto-cleanup: {ex.Message}");
                DebugLog($"Stack trace: {ex.StackTrace}");
            }
        }


        private static bool IsAutoSaveFile(string fileName) {
            // An auto-save is identified by the pattern: P{profileNumber} {date-time} {deckName} Round {roundNumber}.jkr
            // We can check if it matches our file naming pattern
            var pattern = @"^P\d+ \d{4}-\d{2}-\d{2} \d{2}-\d{2}-\d{2}.*Round \d+\.jkr$";
            return Regex.IsMatch(fileName, pattern);
        }

        private void AutoCleanCheckBox_Checked(object sender, RoutedEventArgs e) { UpdateAutoClean(); }

        private void AutoCleanCheckBox_Unchecked(object sender, RoutedEventArgs e) { UpdateAutoClean(); }

        private void CleanupTimeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (IsInitialized && AutoCleanCheckBox.IsChecked == true) { UpdateAutoClean(); }
        }

        private void UpdateAutoClean() {
            if (AutoCleanCheckBox.IsChecked == true) {
                if (CleanupTimeComboBox.SelectedIndex >= 0 &&
                    _cleanupOptions.TryGetValue(CleanupTimeComboBox.SelectedIndex, out TimeSpan selectedTimeSpan)) {
                    _cleanupTimeSpan = selectedTimeSpan;
                    _cleanupTimer.Start();

                    // Run cleanup immediately
                    CleanupOldFiles();

                    SetSuccessStatus($"Auto-clean enabled: files older than {CleanupTimeComboBox.Text}");
                    DebugLog($"Auto-clean enabled: files older than {CleanupTimeComboBox.Text}");
                } else {
                    AutoCleanCheckBox.IsChecked = false;
                    SetErrorStatus("Invalid cleanup time selection");
                    DebugLog("Auto-clean failed: Invalid cleanup time selection.");
                }
            } else {
                _cleanupTimer.Stop();
                SetSuccessStatus("Auto-clean disabled");
                DebugLog("Auto-clean disabled.");
            }
        }

        // Debug logging helper
        private void DebugLog(string message) {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string entry = $"[{timestamp}] {message}";
            _debugLog.Add(entry);
            if (_debugWindow != null) { _debugWindow.AppendLog(entry); }
        }


        // Debug window checkbox handlers
        private void ShowDebugWindowCheckBox_Checked(object sender, RoutedEventArgs e) {
            if (_debugWindow == null) {
                _debugWindow = new DebugWindow();
                _debugWindow.Owner = this;
                _debugWindow.SetLog(_debugLog);
                _debugWindow.Closed += DebugWindow_Closed;
                _debugWindow.Show();
            } else {
                _debugWindow.Show();
                _debugWindow.Activate();
            }
        }

        private void ShowDebugWindowCheckBox_Unchecked(object sender, RoutedEventArgs e) {
            if (_debugWindow != null) {
                _debugWindow.Close();
                _debugWindow = null;
            }
        }

        private void DebugWindow_Closed(object? sender, EventArgs e) {
            ShowDebugWindowCheckBox.IsChecked = false;
            _debugWindow = null;
        }
    }
}