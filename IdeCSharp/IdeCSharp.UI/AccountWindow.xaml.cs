using System.Diagnostics;
using System.Windows;
using System.Windows.Media.Imaging;
using IdeCSharp.Core.Accounts;

namespace IdeCSharp.UI;

/// <summary>Fenêtre de connexion à un compte GitHub (Device Flow).</summary>
public partial class AccountWindow : Window
{
    private readonly GitHubAuthService _auth = new();
    private CancellationTokenSource? _cts;
    private DeviceCodeInfo? _info;

    public AccountWindow()
    {
        InitializeComponent();

        // Barre de titre sombre ou claire selon le thème
        SourceInitialized += (_, _) => WindowStyler.SetDarkTitleBar(this, ThemeManager.IsDark);

        // Si la fenêtre se ferme pendant l'attente, on arrête de contacter GitHub
        Closed += (_, _) => _cts?.Cancel();

        ShowAccount(GitHubAuthService.LoadSaved());
    }

    // Affiche l'écran « connecté » ou « déconnecté » selon le compte
    private void ShowAccount(GitHubAccount? account)
    {
        WaitingPanel.Visibility = Visibility.Collapsed;
        SignedOutPanel.Visibility = account is null ? Visibility.Visible : Visibility.Collapsed;
        SignedInPanel.Visibility = account is null ? Visibility.Collapsed : Visibility.Visible;

        if (account is null) return;

        NameText.Text = string.IsNullOrWhiteSpace(account.Name) ? account.Login : account.Name;
        LoginText.Text = "@" + account.Login;

        // L'avatar se charge depuis internet : s'il échoue, on garde le cercle vide
        if (!string.IsNullOrEmpty(account.AvatarUrl))
        {
            try { AvatarBrush.ImageSource = new BitmapImage(new Uri(account.AvatarUrl)); }
            catch { /* pas d'avatar, ce n'est pas grave */ }
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    // async void : gestionnaire de clic, toutes les erreurs sont attrapées ici
    private async void OnSignInClick(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;
        SignInButton.IsEnabled = false;
        _cts = new CancellationTokenSource();

        try
        {
            _info = await _auth.RequestDeviceCodeAsync(_cts.Token);

            CodeText.Text = _info.UserCode;
            SignedOutPanel.Visibility = Visibility.Collapsed;
            WaitingPanel.Visibility = Visibility.Visible;
            OpenBrowser(_info.VerificationUri);

            var account = await _auth.WaitForAuthorizationAsync(_info, _cts.Token);
            GitHubAuthService.Save(account);
            ShowAccount(account);
        }
        catch (OperationCanceledException)
        {
            ShowAccount(GitHubAuthService.LoadSaved());
        }
        catch (Exception ex)
        {
            ShowAccount(GitHubAuthService.LoadSaved());
            ShowError(ex.Message);
        }
        finally
        {
            SignInButton.IsEnabled = true;
        }
    }

    private static void OpenBrowser(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { /* l'utilisateur peut ouvrir la page lui-même */ }
    }

    private void OnReopenClick(object sender, RoutedEventArgs e)
    {
        if (_info is not null) OpenBrowser(_info.VerificationUri);
    }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(CodeText.Text); }
        catch { /* le presse-papiers peut être occupé : on ignore */ }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => _cts?.Cancel();

    private void OnSignOutClick(object sender, RoutedEventArgs e)
    {
        GitHubAuthService.SignOut();
        ShowAccount(null);
    }
}