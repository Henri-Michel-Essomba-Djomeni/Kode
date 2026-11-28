using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using IdeCSharp.Core.Interfaces;
using IdeCSharp.Core.Models;

namespace IdeCSharp.UI
{
    /// <summary>
    /// Explorateur de fichiers (T27) avec menu clic droit (T28), responsable : Raïssa.
    /// Se branche au routeur : affiche le dossier choisi (FolderOpened) et ouvre un fichier au double-clic.
    /// </summary>
    public sealed class FileExplorer
    {
        private readonly MainWindow _window;
        private readonly IFileService _files;
        private readonly TreeView _tree;
        private TreeViewItem? _rootItem;

        // Mise à jour automatique (T32) : on surveille le disque et on rafraîchit l'arbre.
        private FileSystemWatcher? _watcher;
        private readonly DispatcherTimer _refreshTimer;
        private readonly HashSet<string> _pendingDirs = new(StringComparer.OrdinalIgnoreCase);
        private HashSet<string>? _restoreExpanded; // dossiers à re-déplier pendant un rechargement

        // Dossiers ignorés par la surveillance : une compilation y change des centaines de fichiers.
        private static readonly string[] IgnoredFolders = { "bin", "obj", ".git", ".vs" };

        public FileExplorer(MainWindow window, IFileService files)
        {
            _window = window;
            _files = files;

            _tree = new TreeView { BorderThickness = new Thickness(0), Background = Brushes.Transparent };
            _tree.PreviewMouseLeftButtonUp += OnClick;  // un seul clic ouvre le fichier, comme dans VS Code
            _tree.PreviewMouseRightButtonDown += OnRightClick;
            _tree.ContextMenu = BuildContextMenu();

            // On attend 400 ms de calme avant de rafraîchir : une copie de 50 fichiers = un seul rafraîchissement.
            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _refreshTimer.Tick += OnRefreshTick;
            _window.Closed += (_, _) => _watcher?.Dispose();

            // Branchement du module Fichiers sur le routeur de Michel
            _window.Router.Files = files;
            _window.Router.FolderOpened += ShowFolder;

            // Tant qu'aucun dossier n'est ouvert, on affiche une invitation.
            _window.SetExplorerContent(new TextBlock
            {
                Text = "Aucun dossier ouvert.\nFichier → Ouvrir un dossier (Ctrl+Maj+O)",
                Margin = new Thickness(12),
                TextWrapping = TextWrapping.Wrap
            });
        }

        private TreeViewItem? SelectedItem => _tree.SelectedItem as TreeViewItem;

        /// <summary>Affiche le dossier choisi comme racine de l'arbre.</summary>
        private void ShowFolder(string folder)
        {
            _tree.Items.Clear();

            string name = Path.GetFileName(folder.TrimEnd('\\', '/'));
            _rootItem = CreateFolderItem(new FileNode(name.Length > 0 ? name : folder, folder, true));
            _tree.Items.Add(_rootItem);
            _rootItem.IsExpanded = true; // déclenche le chargement du premier niveau

            _window.SetExplorerContent(_tree);
            StartWatching(folder);
        }

        // ------------------------------------------------------------------
        // Chargement paresseux : on ne lit le contenu d'un dossier que lorsqu'on le déplie.
        // ------------------------------------------------------------------

        private TreeViewItem CreateFolderItem(FileNode node)
        {
            var item = new TreeViewItem { Header = CreateHeader(node.Name, "📁", FolderBrush), Tag = node };
            item.Items.Add("…"); // élément factice : fait apparaître la flèche de dépliage
            item.Expanded += OnExpanded;

            // Pendant un rechargement, on re-déplie les dossiers qui l'étaient avant.
            if (_restoreExpanded?.Contains(node.FullPath) == true)
                item.IsExpanded = true;

            return item;
        }

        // ------------------------------------------------------------------
        // Icônes par type de fichier (T31)
        // Couleurs de milieu de gamme : lisibles sur le thème clair comme sur le thème sombre.
        // ------------------------------------------------------------------

        private static readonly Brush FolderBrush = MakeBrush(0xDC, 0xB6, 0x7A);
        private static readonly Brush PurpleBrush = MakeBrush(0xA3, 0x71, 0xF7);
        private static readonly Brush GoldBrush = MakeBrush(0xD4, 0xA7, 0x2C);
        private static readonly Brush OrangeBrush = MakeBrush(0xE8, 0x83, 0x3A);
        private static readonly Brush GrayBrush = MakeBrush(0x8B, 0x94, 0x9E);

        private static Brush MakeBrush(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze(); // un pinceau figé est plus léger et partageable
            return brush;
        }

        private static TreeViewItem CreateFileItem(FileNode node)
        {
            var (glyph, brush, bold) = IconFor(node.Name);
            return new TreeViewItem { Header = CreateHeader(node.Name, glyph, brush, bold), Tag = node };
        }

        /// <summary>Choisit le symbole et la couleur d'après l'extension du fichier.</summary>
        private static (string Glyph, Brush? Brush, bool Bold) IconFor(string fileName) =>
            Path.GetExtension(fileName).ToLowerInvariant() switch
            {
                ".cs" => ("C#", PurpleBrush, true),
                ".csproj" or ".sln" or ".slnx" => ("⚙", PurpleBrush, false),
                ".json" => ("{}", GoldBrush, true),
                ".xml" or ".xaml" or ".config" => ("</>", OrangeBrush, true),
                ".md" or ".txt" => ("📝", null, false),
                ".png" or ".jpg" or ".jpeg" or ".gif" or ".svg" or ".ico" => ("🖼", null, false),
                ".exe" or ".dll" => ("⚙", GrayBrush, false),
                _ => ("📄", null, false)
            };

        /// <summary>Construit « icône + nom » pour une ligne de l'arbre.</summary>
        private static StackPanel CreateHeader(string name, string glyph, Brush? glyphBrush, bool bold = false)
        {
            var icon = new TextBlock
            {
                Text = glyph,
                Width = 26,
                Margin = new Thickness(0, 0, 4, 0),
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = bold ? 11 : 13,
                FontWeight = bold ? FontWeights.Bold : FontWeights.Normal
            };
            if (glyphBrush != null)
                icon.Foreground = glyphBrush;

            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(icon);
            panel.Children.Add(new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center });
            return panel;
        }

        private void OnExpanded(object sender, RoutedEventArgs e)
        {
            // L'événement remonte aux parents : on ne traite que l'élément réellement déplié.
            if (!ReferenceEquals(e.OriginalSource, sender)) return;

            var item = (TreeViewItem)sender;
            if (item.Items.Count == 1 && item.Items[0] is string) // pas encore chargé
            {
                item.Items.Clear();
                LoadChildren(item);
            }
        }

        private void LoadChildren(TreeViewItem folderItem)
        {
            var folder = (FileNode)folderItem.Tag;

            try
            {
                foreach (var child in _files.ListDirectory(folder.FullPath))
                {
                    folderItem.Items.Add(child.IsDirectory
                        ? CreateFolderItem(child)
                        : CreateFileItem(child));
                }
            }
            catch (IOException ex) // inclut DirectoryNotFoundException
            {
                _window.SetStatus($"Impossible de lire « {folder.Name} » : {ex.Message}");
            }
        }

        /// <summary>Relit le contenu d'un dossier de l'arbre (après création, renommage, suppression).</summary>
        private void ReloadFolder(TreeViewItem folderItem, bool expand = true)
        {
            // On mémorise les sous-dossiers ouverts pour ne pas tout replier à chaque rechargement.
            var expanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectExpanded(folderItem, expanded);

            _restoreExpanded = expanded;
            try
            {
                folderItem.Items.Clear();
                LoadChildren(folderItem);
            }
            finally
            {
                _restoreExpanded = null;
            }

            if (expand)
                folderItem.IsExpanded = true;
        }

        private static void CollectExpanded(TreeViewItem folderItem, HashSet<string> result)
        {
            foreach (var child in folderItem.Items.OfType<TreeViewItem>())
            {
                if (child.IsExpanded && child.Tag is FileNode { IsDirectory: true } node)
                {
                    result.Add(node.FullPath);
                    CollectExpanded(child, result);
                }
            }
        }

        // ------------------------------------------------------------------
        // Clic : ouvre le fichier dans l'éditeur (via le routeur)
        // ------------------------------------------------------------------

        // async void est acceptable ici : c'est un gestionnaire d'événement, avec try/catch.
        private async void OnClick(object sender, MouseButtonEventArgs e)
        {
            // Un dossier se déplie tout seul : on ne s'occupe que des fichiers
            if (SelectedItem is not { Tag: FileNode { IsDirectory: false } file } item) return;
            if (!ReferenceEquals(FindTreeViewItem(e.OriginalSource as DependencyObject), item)) return; // clic dans le vide

            try
            {
                await _window.Router.OpenPathAsync(file.FullPath);
            }
            catch (Exception ex)
            {
                _window.SetStatus($"Erreur : {ex.Message}");
            }
        }

        // ------------------------------------------------------------------
        // Menu clic droit (T28)
        // ------------------------------------------------------------------

        private ContextMenu BuildContextMenu()
        {
            var menu = new ContextMenu();
            var rename = MakeItem("Renommer…", Rename);
            var delete = MakeItem("Supprimer", Delete);

            menu.Items.Add(MakeItem("Nouveau fichier…", NewFile));
            menu.Items.Add(MakeItem("Nouveau dossier…", NewFolder));
            menu.Items.Add(new Separator());
            menu.Items.Add(rename);
            menu.Items.Add(delete);
            menu.Items.Add(new Separator());
            menu.Items.Add(MakeItem("Actualiser", Refresh));

            // Renommer et Supprimer n'ont pas de sens sur le dossier racine ni dans le vide.
            menu.Opened += (_, _) =>
            {
                bool canEdit = SelectedItem is not null && !ReferenceEquals(SelectedItem, _rootItem);
                rename.IsEnabled = canEdit;
                delete.IsEnabled = canEdit;
            };
            return menu;
        }

        private static MenuItem MakeItem(string header, Action action)
        {
            var item = new MenuItem { Header = header };
            item.Click += (_, _) => action();
            return item;
        }

        /// <summary>Un clic droit sélectionne d'abord l'élément visé (WPF ne le fait pas tout seul).</summary>
        private void OnRightClick(object sender, MouseButtonEventArgs e)
        {
            var item = FindTreeViewItem(e.OriginalSource as DependencyObject);
            if (item != null)
                item.IsSelected = true;
            else if (SelectedItem != null)
                SelectedItem.IsSelected = false; // clic dans le vide : on désélectionne
        }

        /// <summary>Dossier où créer un élément : le dossier sélectionné, ou le parent du fichier sélectionné.</summary>
        private TreeViewItem? TargetFolder()
        {
            var selected = SelectedItem;
            if (selected is null) return _rootItem;
            if (selected.Tag is FileNode { IsDirectory: true }) return selected;
            return ParentFolder(selected) ?? _rootItem;
        }

        private static TreeViewItem? ParentFolder(TreeViewItem item) =>
            ItemsControl.ItemsControlFromItemContainer(item) as TreeViewItem;

        private void NewFile()
        {
            if (TargetFolder() is not { Tag: FileNode folder } folderItem) return;

            string? name = PromptDialog.Ask(_window, "Nouveau fichier", "Nom du fichier :", "NouveauFichier.cs");
            if (name is null) return;
            if (!IsValidName(name)) { _window.SetStatus("Nom invalide."); return; }

            Guard(() =>
            {
                _files.CreateFile(Path.Combine(folder.FullPath, name));
                ReloadFolder(folderItem);
                _window.SetStatus($"Fichier créé : {name}");
            });
        }

        private void NewFolder()
        {
            if (TargetFolder() is not { Tag: FileNode folder } folderItem) return;

            string? name = PromptDialog.Ask(_window, "Nouveau dossier", "Nom du dossier :", "NouveauDossier");
            if (name is null) return;
            if (!IsValidName(name)) { _window.SetStatus("Nom invalide."); return; }

            Guard(() =>
            {
                _files.CreateFolder(Path.Combine(folder.FullPath, name));
                ReloadFolder(folderItem);
                _window.SetStatus($"Dossier créé : {name}");
            });
        }

        private void Rename()
        {
            if (SelectedItem is not { Tag: FileNode node } item || ReferenceEquals(item, _rootItem)) return;

            string? newName = PromptDialog.Ask(_window, "Renommer", "Nouveau nom :", node.Name);
            if (newName is null || newName == node.Name) return;
            if (!IsValidName(newName)) { _window.SetStatus("Nom invalide."); return; }

            Guard(() =>
            {
                _files.Rename(node.FullPath, newName);
                if (ParentFolder(item) is { } parent) ReloadFolder(parent);
                _window.SetStatus($"Renommé : {node.Name} → {newName}");
            });
        }

        private void Delete()
        {
            if (SelectedItem is not { Tag: FileNode node } item || ReferenceEquals(item, _rootItem)) return;

            string message = $"Supprimer « {node.Name} » ?"
                + (node.IsDirectory ? "\nLe dossier et tout son contenu seront supprimés." : "")
                + "\nCette action est définitive.";
            var answer = MessageBox.Show(_window, message, "Confirmer la suppression",
                MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (answer != MessageBoxResult.Yes) return;

            Guard(() =>
            {
                _files.Delete(node.FullPath);
                if (ParentFolder(item) is { } parent) ReloadFolder(parent);
                _window.SetStatus($"Supprimé : {node.Name}");
            });
        }

        private void Refresh()
        {
            if (TargetFolder() is { } folderItem)
                ReloadFolder(folderItem);
        }

        // ------------------------------------------------------------------
        // Mise à jour automatique de l'arbre (T32)
        // ------------------------------------------------------------------

        /// <summary>Surveille le dossier ouvert : un fichier créé, supprimé ou renommé ailleurs apparaît dans l'arbre.</summary>
        private void StartWatching(string folder)
        {
            _watcher?.Dispose();
            _watcher = null;
            _pendingDirs.Clear();

            try
            {
                var watcher = new FileSystemWatcher(folder)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
                };
                watcher.Created += OnDiskChanged;
                watcher.Deleted += OnDiskChanged;
                watcher.Renamed += (s, e) =>
                {
                    OnDiskChanged(s, e);
                    QueueRefresh(Path.GetDirectoryName(e.OldFullPath)); // l'ancien emplacement aussi
                };
                watcher.Error += (_, _) => QueueRefresh(folder); // trop d'événements d'un coup : on relit tout
                watcher.EnableRaisingEvents = true;
                _watcher = watcher;
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
            {
                // Pas de surveillance possible : l'arbre reste utilisable, avec « Actualiser » à la main.
                _window.SetStatus($"Mise à jour automatique indisponible : {ex.Message}");
            }
        }

        // Ces événements arrivent sur un thread de fond : on ne touche JAMAIS à l'interface ici.
        private void OnDiskChanged(object sender, FileSystemEventArgs e) =>
            QueueRefresh(Path.GetDirectoryName(e.FullPath));

        private void QueueRefresh(string? directory)
        {
            if (string.IsNullOrEmpty(directory) || IsIgnored(directory)) return;

            // On repasse sur le thread de l'interface, et on relance le minuteur (anti-rebond).
            _window.Dispatcher.BeginInvoke(new Action(() =>
            {
                _pendingDirs.Add(directory);
                _refreshTimer.Stop();
                _refreshTimer.Start();
            }));
        }

        private void OnRefreshTick(object? sender, EventArgs e)
        {
            _refreshTimer.Stop();

            // Les dossiers parents d'abord : leur rechargement recrée les enfants.
            var directories = _pendingDirs.OrderBy(d => d.Length).ToList();
            _pendingDirs.Clear();

            if (_rootItem is null) return;

            foreach (var directory in directories)
            {
                if (!Directory.Exists(directory)) continue; // supprimé entre-temps : son parent est déjà rafraîchi

                var item = FindFolderItem(_rootItem, directory);

                // On ne recharge que les dossiers déjà affichés ; les autres se chargeront à l'ouverture.
                if (item != null && !(item.Items.Count == 1 && item.Items[0] is string))
                    ReloadFolder(item, expand: false);
            }
        }

        private static TreeViewItem? FindFolderItem(TreeViewItem from, string path)
        {
            if (from.Tag is not FileNode node) return null;

            if (string.Equals(node.FullPath.TrimEnd('\\', '/'), path.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
                return from;

            foreach (var child in from.Items.OfType<TreeViewItem>())
            {
                if (child.Tag is FileNode { IsDirectory: true } && FindFolderItem(child, path) is { } found)
                    return found;
            }
            return null;
        }

        private static bool IsIgnored(string path) =>
            path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(part => IgnoredFolders.Contains(part, StringComparer.OrdinalIgnoreCase));

        // ------------------------------------------------------------------
        // Outils
        // ------------------------------------------------------------------

        /// <summary>Exécute une opération sur les fichiers en affichant les erreurs dans la barre d'état.</summary>
        private void Guard(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                _window.SetStatus($"Erreur : {ex.Message}");
            }
        }

        /// <summary>Refuse les noms vides, « . », « .. » et ceux qui contiennent des caractères interdits (\ / : * ? ...).</summary>
        private static bool IsValidName(string name) =>
            name.Length > 0 && name != "." && name != ".." && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

        /// <summary>Remonte depuis l'élément cliqué jusqu'au premier élément de l'arbre.</summary>
        private static TreeViewItem? FindTreeViewItem(DependencyObject? source)
        {
            while (source != null)
            {
                if (source is TreeViewItem item) return item;

                source = source is Visual or System.Windows.Media.Media3D.Visual3D
                    ? VisualTreeHelper.GetParent(source)
                    : LogicalTreeHelper.GetParent(source);
            }
            return null;
        }
    }

    /// <summary>Petite boîte de dialogue « saisir un nom » (WPF n'en fournit pas).</summary>
    internal static class PromptDialog
    {
        /// <returns>Le texte saisi, ou null si l'utilisateur annule ou ne tape rien.</returns>
        public static string? Ask(Window owner, string title, string label, string defaultText = "")
        {
            var box = new TextBox { Text = defaultText, Margin = new Thickness(0, 6, 0, 12), MinWidth = 280 };
            var ok = new Button { Content = "OK", IsDefault = true, Width = 80, Margin = new Thickness(0, 0, 8, 0) };
            var cancel = new Button { Content = "Annuler", IsCancel = true, Width = 80 };

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);

            var panel = new StackPanel { Margin = new Thickness(16) };
            panel.Children.Add(new TextBlock { Text = label });
            panel.Children.Add(box);
            panel.Children.Add(buttons);

            var dialog = new Window
            {
                Title = title,
                Owner = owner,
                Content = panel,
                SizeToContent = SizeToContent.WidthAndHeight,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false
            };

            ok.Click += (_, _) => dialog.DialogResult = true;
            dialog.Loaded += (_, _) => { box.Focus(); box.SelectAll(); };

            if (dialog.ShowDialog() != true) return null;

            string text = box.Text.Trim();
            return text.Length > 0 ? text : null;
        }
    }
}
