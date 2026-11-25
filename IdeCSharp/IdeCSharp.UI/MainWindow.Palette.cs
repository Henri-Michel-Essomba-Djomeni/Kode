using System.IO;
using System.Windows;
using System.Windows.Input;

namespace IdeCSharp.UI;

// Palette de commandes (Ctrl+Maj+P) et ouverture rapide d'un fichier (Ctrl+P).
partial class MainWindow
{
    // Dossiers qu'on ne parcourt pas pour l'ouverture rapide (volumineux et sans intérêt)
    private static readonly HashSet<string> SkippedFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".vs", ".idea", "bin", "obj", "node_modules", "__pycache__", "vendor"
    };

    // Les raccourcis sont lus avant les autres contrôles : ils marchent même dans l'éditeur
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Key != Key.P) return;

        var modifiers = Keyboard.Modifiers;
        if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            e.Handled = true;
            ShowCommandPalette();
        }
        else if (modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            ShowQuickOpen();
        }
    }

    private void OnPaletteMenuClick(object sender, RoutedEventArgs e) => ShowCommandPalette();

    private void OnQuickOpenMenuClick(object sender, RoutedEventArgs e) => ShowQuickOpen();

    // ------------------------------------------------------------------
    // Palette de commandes
    // ------------------------------------------------------------------

    private void ShowCommandPalette()
    {
        // La commande s'exécute là où était le focus (l'éditeur, par exemple)
        IInputElement target = Keyboard.FocusedElement ?? this;

        PaletteItem Cmd(string title, string? shortcut, ICommand command) =>
            new(title, shortcut, () => RunCommand(command, target));

        var items = new List<PaletteItem>
        {
            Cmd("Fichier : Nouveau fichier", "Ctrl+N", ApplicationCommands.New),
            Cmd("Fichier : Ouvrir un fichier…", null, ApplicationCommands.Open),
            Cmd("Fichier : Ouvrir un dossier…", "Ctrl+Maj+O", AppCommands.OpenFolder),
            Cmd("Fichier : Enregistrer", null, ApplicationCommands.Save),
            Cmd("Fichier : Enregistrer sous…", null, AppCommands.SaveAs),
            new("Fichier : Cloner un dépôt GitHub…", "Git", OnCloneRepository),

            Cmd("Édition : Rechercher", "Ctrl+F", ApplicationCommands.Find),
            Cmd("Édition : Remplacer", null, ApplicationCommands.Replace),
            Cmd("Édition : Aller à la ligne…", null, AppCommands.GoToLine),

            Cmd("Affichage : Afficher / masquer l'explorateur", "Ctrl+B", AppCommands.ToggleExplorer),
            Cmd("Affichage : Afficher / masquer le panneau du bas", "Ctrl+J", AppCommands.ToggleBottomPanel),
            Cmd("Affichage : Afficher / masquer le panneau de droite", "Ctrl+Alt+B", AppCommands.ToggleRightPanel),
            Cmd("Affichage : Extensions et thèmes", "Ctrl+Maj+X", AppCommands.ShowExtensions),
            Cmd("Affichage : Paramètres", "Ctrl+,", AppCommands.ShowSettings),
            new("Thème : Clair", null, () => ThemeManager.Apply(AppTheme.Light)),
            new("Thème : Sombre", null, () => ThemeManager.Apply(AppTheme.Dark)),
            new("Thème : Comme Windows", null, () => ThemeManager.Apply(AppTheme.System)),

            Cmd("Exécuter : Lancer le programme", "F5", AppCommands.Run),
            Cmd("Exécuter : Arrêter", "Maj+F5", AppCommands.Stop),

            new("Compte : GitHub…", null, () => OnAccountButtonClick(this, new RoutedEventArgs())),
            Cmd("Aide : À propos de Kode", null, AppCommands.About),
        };

        new PaletteWindow(items, "Palette de commandes", this).ShowDialog();
    }

    // Lance une commande comme si elle venait du menu ou du clavier
    private static void RunCommand(ICommand command, IInputElement target)
    {
        if (command is RoutedCommand routed)
        {
            if (routed.CanExecute(null, target)) routed.Execute(null, target);
        }
        else if (command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    // ------------------------------------------------------------------
    // Ouverture rapide d'un fichier du dossier ouvert
    // ------------------------------------------------------------------

    // async void : gestionnaire de raccourci, les erreurs sont gérées par OpenRecent
    private async void ShowQuickOpen()
    {
        var folder = Router.CurrentFolder;
        if (folder is null)
        {
            SetStatus("Ouvre d'abord un dossier (Ctrl+Maj+O).");
            return;
        }

        // On parcourt le disque hors du thread de l'interface : un gros dossier ne fige pas Kode
        var files = await Task.Run(() => ListProjectFiles(folder));

        var items = files
            .Select(file => new PaletteItem(
                Path.GetFileName(file),
                Path.GetRelativePath(folder, file),
                () => OpenRecent(file)))
            .ToList();

        new PaletteWindow(items, "Aller au fichier", this).ShowDialog();
    }

    private static List<string> ListProjectFiles(string root)
    {
        const int Limit = 5000;
        var result = new List<string>();
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0 && result.Count < Limit)
        {
            var folder = pending.Pop();
            try
            {
                foreach (var file in Directory.EnumerateFiles(folder))
                {
                    if (result.Count >= Limit) break;
                    result.Add(file);
                }

                foreach (var sub in Directory.EnumerateDirectories(folder))
                {
                    if (!SkippedFolders.Contains(Path.GetFileName(sub))) pending.Push(sub);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Dossier illisible : on l'ignore
            }
        }

        return result;
    }
}