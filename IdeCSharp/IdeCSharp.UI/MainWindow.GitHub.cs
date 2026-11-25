using System.Windows;
using IdeCSharp.Core.Accounts;

namespace IdeCSharp.UI;

// Fonctions liées au compte GitHub : cloner un dépôt.
partial class MainWindow
{
    // Ouvre la fenêtre de clonage (en demandant de se connecter d'abord si besoin)
    private void OnCloneRepository()
    {
        var account = GitHubAuthService.LoadSaved();
        if (account is null)
        {
            new AccountWindow { Owner = this }.ShowDialog();
            UpdateAccountButton();
            RefreshWelcomeAccount();

            account = GitHubAuthService.LoadSaved();
            if (account is null) return;
        }

        var window = new CloneRepoWindow(account) { Owner = this };
        if (window.ShowDialog() == true && window.ClonedPath is { } path)
        {
            Router.OpenFolderPath(path);
        }
    }

    private void OnCloneMenuClick(object sender, RoutedEventArgs e) => OnCloneRepository();
}