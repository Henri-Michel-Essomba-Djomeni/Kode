using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using IdeCSharp.Core.Accounts;
using Microsoft.Win32;

namespace IdeCSharp.UI;

/// <summary>Fenêtre « Cloner un dépôt GitHub » : liste, URL libre, destination, progression.</summary>
public partial class CloneRepoWindow : Window
{
    private static readonly Regex SafeName = new(@"^[A-Za-z0-9_.-]+$", RegexOptions.Compiled);

    private readonly GitHubAccount _account;
    private List<GitHubRepo> _all = new();
    private CancellationTokenSource? _cts;
    private bool _busy;

    /// <summary>Dossier du dépôt cloné (renseigné quand le clonage a réussi).</summary>
    public string? ClonedPath { get; private set; }

    public CloneRepoWindow(GitHubAccount account)
    {
        InitializeComponent();
        _account = account;

        AccountText.Text = "Compte : @" + account.Login;
        DestBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Kode");

        SourceInitialized += (_, _) => WindowStyler.SetDarkTitleBar(this, ThemeManager.IsDark);
        Loaded += async (_, _) => await LoadReposAsync();
        Closed += (_, _) => _cts?.Cancel();
    }

    // ------------------------------------------------------------------
    // Liste des dépôts
    // ------------------------------------------------------------------

    private async Task LoadReposAsync()
    {
        ListMessage.Text = "Chargement de tes dépôts…";
        ListMessage.Visibility = Visibility.Visible;

        try
        {
            _cts = new CancellationTokenSource();
            _all = (await GitHubRepoService.ListPublicReposAsync(_account.Login, _cts.Token)).ToList();
            ApplyFilter();
        }
        catch (OperationCanceledException)
        {
            // fenêtre fermée pendant le chargement
        }
        catch (InvalidOperationException ex)
        {
            ListMessage.Text = ex.Message;
        }
    }

    private void ApplyFilter()
    {
        string text = SearchBox.Text.Trim();
        var shown = _all
            .Where(r => text.Length == 0
                || r.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
                || (r.Description?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false))
            .ToList();

        RepoList.ItemsSource = shown;

        if (shown.Count == 0)
        {
            ListMessage.Text = _all.Count == 0
                ? "Aucun dépôt public trouvé. Colle l'URL d'un dépôt ci-dessous."
                : "Aucun dépôt ne correspond à ta recherche.";
            ListMessage.Visibility = Visibility.Visible;
        }
        else
        {
            ListMessage.Visibility = Visibility.Collapsed;
        }
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        if (IsLoaded) ApplyFilter();
    }

    // Choisir un dépôt dans la liste efface l'URL, et inversement : un seul choix à la fois
    private void OnRepoSelected(object sender, SelectionChangedEventArgs e)
    {
        if (RepoList.SelectedItem is not null) UrlBox.Text = string.Empty;
    }

    private void OnUrlChanged(object sender, TextChangedEventArgs e)
    {
        if (UrlBox.Text.Length > 0) RepoList.SelectedItem = null;
    }

    // ------------------------------------------------------------------
    // Destination et clonage
    // ------------------------------------------------------------------

    private void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Dossier de destination" };
        if (dialog.ShowDialog(this) == true) DestBox.Text = dialog.FolderName;
    }

    // async void : gestionnaire de clic, toutes les erreurs sont attrapées ici
    private async void OnCloneClick(object sender, RoutedEventArgs e)
    {
        string cloneUrl, name;

        if (!string.IsNullOrWhiteSpace(UrlBox.Text))
        {
            if (!TryParseGitHubUrl(UrlBox.Text.Trim(), out cloneUrl, out name))
            {
                StatusText.Text = "URL invalide. Exemple : https://github.com/pseudo/depot";
                return;
            }
        }
        else if (RepoList.SelectedItem is GitHubRepo repo && IsSafeName(repo.Name))
        {
            cloneUrl = repo.CloneUrl;
            name = repo.Name;
        }
        else
        {
            StatusText.Text = "Choisis un dépôt dans la liste ou colle une URL.";
            return;
        }

        string parent = DestBox.Text.Trim();
        if (parent.Length == 0)
        {
            StatusText.Text = "Choisis un dossier de destination.";
            return;
        }

        string target = Path.Combine(parent, name);
        try
        {
            if (Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any())
            {
                StatusText.Text = $"Le dossier « {target} » existe déjà et n'est pas vide.";
                return;
            }
            Directory.CreateDirectory(parent);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            StatusText.Text = ex.Message;
            return;
        }

        SetBusy(true);
        _cts = new CancellationTokenSource();
        try
        {
            // Progress<T> créé ici : les mises à jour reviennent sur le thread de l'interface
            var progress = new Progress<string>(line => StatusText.Text = line);
            await GitCloneService.CloneAsync(cloneUrl, target, progress, _cts.Token);

            ClonedPath = target;
            DialogResult = true;
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Clonage annulé.";
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        if (_busy) _cts?.Cancel();
        else Close();
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        Busy.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        CloneButton.IsEnabled = !busy;
        RepoList.IsEnabled = !busy;
        UrlBox.IsEnabled = !busy;
        DestBox.IsEnabled = !busy;
        CancelButton.Content = busy ? "Annuler" : "Fermer";
    }

    // ------------------------------------------------------------------
    // Validation : on n'accepte que github.com et des noms simples
    // ------------------------------------------------------------------

    private static bool IsSafeName(string name) =>
        SafeName.IsMatch(name) && name.Trim('.').Length > 0;

    private static bool TryParseGitHubUrl(string text, out string cloneUrl, out string name)
    {
        cloneUrl = name = string.Empty;

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttps || !(uri.Host is "github.com" or "www.github.com")) return false;

        var parts = uri.AbsolutePath.Trim('/').Split('/');
        if (parts.Length < 2) return false;

        string owner = parts[0];
        string repo = parts[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? parts[1][..^4] : parts[1];
        if (!IsSafeName(owner) || !IsSafeName(repo)) return false;

        cloneUrl = $"https://github.com/{owner}/{repo}.git";
        name = repo;
        return true;
    }
}