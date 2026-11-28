using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using IdeCSharp.Core.Accounts;

namespace IdeCSharp.UI;

/// <summary>Écran d'accueil : cartes d'action et liste des éléments récents.</summary>
public partial class WelcomeView : UserControl
{
    public WelcomeView()
    {
        InitializeComponent();
    }

    /// <summary>Levé quand l'utilisateur clique sur un élément récent (fichier ou dossier).</summary>
    public event Action<string>? RecentRequested;

    /// <summary>Levé quand l'utilisateur clique sur « Cloner un dépôt GitHub ».</summary>
    public event Action? CloneRequested;

    private void OnCloneClick(object sender, RoutedEventArgs e) => CloneRequested?.Invoke();

    /// <summary>Affiche la salutation et l'avatar du compte GitHub connecté (null : masqués).</summary>
    public void SetAccount(GitHubAccount? account)
    {
        GreetingPanel.Visibility = account is null ? Visibility.Collapsed : Visibility.Visible;
        if (account is null) return;

        // Prénom = premier mot du nom complet, sinon le pseudo
        string name = string.IsNullOrWhiteSpace(account.Name)
            ? account.Login
            : account.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        GreetingText.Text = $"Bonjour, {name}";

        if (!string.IsNullOrEmpty(account.AvatarUrl))
        {
            try { GreetingAvatar.ImageSource = new BitmapImage(new Uri(account.AvatarUrl)); }
            catch { /* pas d'avatar : le cercle reste vide */ }
        }
    }

    /// <summary>Affiche les éléments récents (6 au maximum). Une liste vide masque la section.</summary>
    public void SetRecents(IEnumerable<string> paths)
    {
        RecentsPanel.Children.Clear();

        foreach (var path in paths.Take(6))
        {
            string clean = path.TrimEnd('\\', '/');
            string name = Path.GetFileName(clean);
            if (name.Length == 0) name = path;

            var nameText = new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center };
            nameText.SetResourceReference(TextBlock.ForegroundProperty, "PanelForegroundBrush");

            // Dossier parent en gris, coupé par « … » s'il est trop long
            var folderText = new TextBlock
            {
                Text = Path.GetDirectoryName(clean) ?? string.Empty,
                FontSize= 12,
                Margin = new Thickness(10, 0, 0, 0),
                MaxWidth = 240,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            folderText.SetResourceReference(TextBlock.ForegroundProperty, "MutedForegroundBrush");

            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(nameText);
            row.Children.Add(folderText);

            var button = new Button { Content = row, ToolTip = path };
            button.SetResourceReference(FrameworkElement.StyleProperty, "WelcomeLinkStyle");
            string target = path;
            button.Click += (_, _) => RecentRequested?.Invoke(target);

            RecentsPanel.Children.Add(button);
        }

        RecentsTitle.Visibility = RecentsPanel.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}