using System.Windows;
using System.Windows.Media;
using IdeCSharp.Core.Extensions;

namespace IdeCSharp.UI
{
    /// <summary>Une extension telle qu'affichée dans la liste (avec son état).</summary>
    public class ExtensionItem
    {
        public ExtensionItem(ExtensionInfo info, bool isInstalled, bool isActive)
        {
            Info = info;
            IsInstalled = isInstalled;
            IsActive = isActive;
        }

        public ExtensionInfo Info { get; }
        public bool IsInstalled { get; }
        public bool IsActive { get; }

        public string Name => Info.Name;
        public string Subtitle => $"{Info.Author} · v{Info.Version} · {(Info.Dark ? "sombre" : "clair")}";
        public string Description => Info.Description;

        public string PrimaryLabel => IsActive ? "Actif" : IsInstalled ? "Appliquer" : "Installer";
        public bool CanApply => !IsActive;
        public Visibility UninstallVisibility => IsInstalled ? Visibility.Visible : Visibility.Collapsed;

        // Trois pastilles de couleur pour avoir un aperçu du thème
        public Brush PreviewBackground => Preview("WindowBackgroundBrush");
        public Brush PreviewPanel => Preview("PanelBackgroundBrush");
        public Brush PreviewAccent => Preview("AccentBrush");

        private Brush Preview(string key) =>
            ThemeManager.TryCreateBrush(Info.Colors.GetValueOrDefault(key, "")) ?? Brushes.Gray;
    }
}