using System;
using System.Linq;
using System.Windows;
using Microsoft.Win32;

namespace IdeCSharp.UI
{
    public enum AppTheme
    {
        Light,
        Dark,
        System
    }

    /// <summary>
    /// Gère le thème clair / sombre de toute l'application.
    /// Deux mécanismes travaillent ensemble : le thème Fluent de WPF stylise les
    /// contrôles standard, et nos dictionnaires Light / Dark fournissent les
    /// couleurs que Kode dessine lui-même (panneaux, séparateurs...).
    /// </summary>
    public static class ThemeManager
    {
        public static AppTheme Current { get; private set; } = AppTheme.System;

        /// <summary>Levé après chaque changement ; le paramètre vaut true si le thème est sombre.</summary>
        public static event Action<bool>? ThemeChanged;

        public static void Apply(AppTheme theme)
        {
            Current = theme;
            bool dark = theme == AppTheme.Dark || (theme == AppTheme.System && SystemUsesDarkMode());
            var app = Application.Current;

            // ThemeMode est une API expérimentale de WPF (disponible depuis .NET 9),
            // d'où le pragma. Si elle disparaît un jour, seul cet endroit est à changer.
#pragma warning disable WPF0001
            app.ThemeMode = dark ? ThemeMode.Dark : ThemeMode.Light;
#pragma warning restore WPF0001

            // On remplace notre dictionnaire de couleurs (et lui seul)
            var dictionaries = app.Resources.MergedDictionaries;
            var previous = dictionaries.FirstOrDefault(d =>
                d.Source != null && d.Source.OriginalString.StartsWith("Themes/", StringComparison.OrdinalIgnoreCase));
            if (previous != null)
            {
                dictionaries.Remove(previous);
            }

            dictionaries.Add(new ResourceDictionary
            {
                Source = new Uri(dark ? "Themes/Dark.xaml" : "Themes/Light.xaml", UriKind.Relative)
            });

            ThemeChanged?.Invoke(dark);
        }

        // Lit le réglage « mode des applications » de Windows
        private static bool SystemUsesDarkMode()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
            }
            catch
            {
                // Dans le doute, on reste en clair
                return false;
            }
        }
    }
}
