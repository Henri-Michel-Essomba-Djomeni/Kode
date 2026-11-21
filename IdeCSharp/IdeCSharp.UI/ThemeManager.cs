using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using IdeCSharp.Core.Extensions;
using IdeCSharp.Core.Settings;
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
    /// Gère le thème de toute l'application. Trois couches travaillent ensemble :
    /// le thème Fluent de WPF (contrôles standard), nos dictionnaires Light / Dark
    /// (couleurs de base de Kode) et, si une extension est active, ses couleurs
    /// posées par-dessus.
    /// </summary>
    public static class ThemeManager
    {
        public static AppTheme Current { get; private set; } = AppTheme.System;

        /// <summary>Vrai si le thème effectivement affiché est sombre.</summary>
        public static bool IsDark { get; private set; }

        /// <summary>Identifiant de l'extension de thème active, ou null.</summary>
        public static string? ActivePackId { get; private set; }

        /// <summary>Levé après chaque changement ; le paramètre vaut true si le thème est sombre.</summary>
        public static event Action<bool>? ThemeChanged;

        // Dictionnaire des couleurs de l'extension active (ajouté après celui de base)
        private static ResourceDictionary? _packDictionary;

        /// <summary>Applique un thème de base. Retire l'extension de thème éventuelle.</summary>
        public static void Apply(AppTheme theme) => ApplyCore(theme, null, persist: true);

        /// <summary>Applique les couleurs d'une extension de thème.</summary>
        public static void ApplyPack(ExtensionInfo pack) =>
            ApplyCore(pack.Dark ? AppTheme.Dark : AppTheme.Light, pack, persist: true);

        /// <summary>Au démarrage : retrouve le thème et l'extension choisis la dernière fois.</summary>
        public static void Restore(ExtensionService extensions)
        {
            var settings = UserSettings.Load();

            if (settings.ActiveExtensionId is not null)
            {
                var pack = extensions.GetInstalled().FirstOrDefault(e => e.Id == settings.ActiveExtensionId);
                if (pack is not null)
                {
                    ApplyCore(pack.Dark ? AppTheme.Dark : AppTheme.Light, pack, persist: false);
                    return;
                }
            }

            var theme = Enum.TryParse<AppTheme>(settings.Theme, out var saved) ? saved : AppTheme.System;
            ApplyCore(theme, null, persist: false);
        }

        private static void ApplyCore(AppTheme theme, ExtensionInfo? pack, bool persist)
        {
            Current = theme;
            ActivePackId = pack?.Id;
            bool dark = theme == AppTheme.Dark || (theme == AppTheme.System && SystemUsesDarkMode());
            IsDark = dark;
            var app = Application.Current;

            // ThemeMode est une API expérimentale de WPF (disponible depuis .NET 9),
            // d'où le pragma. Si elle disparaît un jour, seul cet endroit est à changer.
#pragma warning disable WPF0001
            app.ThemeMode = dark ? ThemeMode.Dark : ThemeMode.Light;
#pragma warning restore WPF0001

            var dictionaries = app.Resources.MergedDictionaries;

            // On retire l'ancien dictionnaire de base et l'ancienne extension (eux seuls)
            var previous = dictionaries.FirstOrDefault(d =>
                d.Source != null && d.Source.OriginalString.StartsWith("Themes/", StringComparison.OrdinalIgnoreCase));
            if (previous != null)
            {
                dictionaries.Remove(previous);
            }
            if (_packDictionary != null)
            {
                dictionaries.Remove(_packDictionary);
                _packDictionary = null;
            }

            dictionaries.Add(new ResourceDictionary
            {
                Source = new Uri(dark ? "Themes/Dark.xaml" : "Themes/Light.xaml", UriKind.Relative)
            });

            // Les couleurs de l'extension viennent après : elles l'emportent sur la base
            if (pack != null)
            {
                _packDictionary = BuildOverlay(pack);
                dictionaries.Add(_packDictionary);
            }

            if (persist)
            {
                Save(theme, pack);
            }

            ThemeChanged?.Invoke(dark);
        }

        // Transforme les couleurs d'une extension en pinceaux utilisables par le XAML
        private static ResourceDictionary BuildOverlay(ExtensionInfo pack)
        {
            var dictionary = new ResourceDictionary();
            foreach (var (key, value) in pack.Colors)
            {
                // Seuls les pinceaux sont acceptés : une extension ne peut pas remplacer autre chose
                if (!key.EndsWith("Brush", StringComparison.Ordinal)) continue;

                var brush = TryCreateBrush(value);
                if (brush != null)
                {
                    dictionary[key] = brush;
                }
            }
            return dictionary;
        }

        /// <summary>"#RRGGBB" donne un pinceau uni ; "#RRGGBB,#RRGGBB" un dégradé horizontal.</summary>
        internal static Brush? TryCreateBrush(string value)
        {
            try
            {
                var colors = value
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(text => (Color)ColorConverter.ConvertFromString(text))
                    .ToList();

                if (colors.Count == 1)
                {
                    return new SolidColorBrush(colors[0]);
                }

                if (colors.Count >= 2)
                {
                    var gradient = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
                    for (int i = 0; i < colors.Count; i++)
                    {
                        gradient.GradientStops.Add(new GradientStop(colors[i], i / (double)(colors.Count - 1)));
                    }
                    return gradient;
                }
            }
            catch (Exception)
            {
                // Couleur mal écrite dans le catalogue : on l'ignore plutôt que de planter
            }
            return null;
        }

        private static void Save(AppTheme theme, ExtensionInfo? pack)
        {
            try
            {
                var settings = UserSettings.Load();
                if (pack is null)
                {
                    settings.Theme = theme.ToString();
                }
                settings.ActiveExtensionId = pack?.Id;
                settings.Save();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Mémoriser le thème n'est pas vital : on continue sans
            }
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