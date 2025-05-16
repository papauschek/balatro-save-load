using System.Collections.ObjectModel;
using System.Windows;

namespace BalatroSaveAndLoad
{
    public partial class DebugWindow : Window
    {
        public ObservableCollection<string> DebugLog { get; } = new ObservableCollection<string>();

        public DebugWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        public void SetLog(ObservableCollection<string> log)
        {
            DebugLog.Clear();
            foreach (var entry in log)
                DebugLog.Add(entry);
        }

        public void AppendLog(string entry)
        {
            DebugLog.Add(entry);
            DebugListBox.ScrollIntoView(entry);
        }
    }
}
