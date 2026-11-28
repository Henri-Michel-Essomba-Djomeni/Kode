using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using IdeCSharp.Core.Assistant;

namespace IdeCSharp.UI;

/// <summary>Fenêtre où l'utilisateur colle (ou supprime) sa clé d'accès à l'IA.</summary>
public partial class AssistantKeyWindow : Window
{
    public AssistantKeyWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => WindowStyler.SetDarkTitleBar(this, ThemeManager.IsDark);

        bool hasKey = AssistantKeyStore.Load() is not null;
        RemoveButton.IsEnabled = hasKey;
        StatusText.Text = hasKey ? "Une clé est déjà enregistrée. Colles-en une autre pour la remplacer." : string.Empty;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        string key = KeyBox.Password.Trim();
        if (key.Length < 20)
        {
            StatusText.Text = "Cette clé semble trop courte. Copie-la en entier depuis Google AI Studio.";
            return;
        }

        AssistantKeyStore.Save(key);
        DialogResult = true;
    }

    private void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        AssistantKeyStore.Clear();
        DialogResult = true;
    }

    // Ouvre le lien dans le navigateur par défaut
    private void OnLinkClick(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}