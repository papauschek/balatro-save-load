using System.Configuration;
using System.Data;
using System.Windows;
using Microsoft.Win32;
using System;

namespace BalatroSaveAndLoad
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Detect system theme
            bool isDark = IsSystemInDarkMode();

            // Swap theme dictionary
            var dicts = Resources.MergedDictionaries;
            dicts.Clear();
            if (isDark)
                dicts.Add(new ResourceDictionary { Source = new Uri("Themes/Dark.xaml", UriKind.Relative) });
            else
                dicts.Add(new ResourceDictionary { Source = new Uri("Themes/Light.xaml", UriKind.Relative) });
        }

        private static bool IsSystemInDarkMode()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key != null)
                {
                    object? value = key.GetValue("AppsUseLightTheme");
                    if (value is int i)
                        return i == 0;
                }
            }
            catch { }
            return false; // Default to light
        }
    }
}
