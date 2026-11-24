using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using IdeCSharp.Core.Services;
using System.Windows.Controls;
using IdeCSharp.Core.Accounts;

namespace IdeCSharp.UI
{
    public partial class MainWindow : Window
    {
        // Tailles mémorisées pour retrouver la mise en page après un masquage
        private double _explorerWidth = 260;
        private double _rightPanelWidth = 300;
        private double _bottomPanelHeight = 200;
        private readonly TerminalService _terminal = new();
        private readonly BottomPanelView _bottomTabs = new();

        /// <summary>
        /// Levé pour chaque commande non gérée par la fenêtre elle-même
        /// (Nouveau, Ouvrir, Enregistrer, Exécuter...). Les modules s'abonnent ici.
        /// </summary>
        public event Action<ICommand>? CommandInvoked;

        /// <summary>Aiguille les commandes vers les modules ; chaque module s'y branche.</summary>
        public CommandRouter Router { get; }

        /// <summary>Aperçu réduit du fichier : l'éditeur y envoie le texte et la zone visible.</summary>
        public MinimapView EditorOverview => MinimapPane;

        /// <summary>Assistant du panneau de droite : on lui branche un service d'IA.</summary>
        public AssistantView AssistantPanel => AssistantPane;


        public MainWindow()
        {
            InitializeComponent();
            Router = new CommandRouter(this);
            Router.Recents = new RecentItemsService();
            _ = new FileExplorer(this, new FileService());

            // On place les onglets SORTIE / TERMINAL dans le panneau du bas
            SetBottomPanelContent(_bottomTabs);

            // La console démarre dans le dossier du projet ouvert (Ctrl+Maj+O),
            // ou dans le dossier utilisateur si aucun dossier n'est ouvert.
            _bottomTabs.TerminalPane.Attach(_terminal, () => Router.CurrentFolder);

            // Quand on ouvre un dossier, la console déjà lancée se déplace dedans
            Router.FolderOpened += folder => _bottomTabs.TerminalPane.OnFolderOpened(folder);

            // On ferme proprement la console quand la fenêtre se ferme
            Closed += (_, _) => _terminal.Dispose();
            InitExecutionModule();
            InitEditorModule();
            // Barre de titre sombre ou claire selon le thème
            SourceInitialized += (_, _) => WindowStyler.SetDarkTitleBar(this, ThemeManager.IsDark);
            ThemeManager.ThemeChanged += dark => WindowStyler.SetDarkTitleBar(this, dark);
            UpdateAccountButton();
        }

        // ------------------------------------------------------------------
        // Zones d'accueil : chaque module y place son propre contrôle
        // ------------------------------------------------------------------

        public void SetExplorerContent(UIElement content) => ExplorerHost.Content = content;

        public void SetEditorContent(UIElement content) => EditorHost.Content = content;

        public void SetBottomPanelContent(UIElement content) => BottomHost.Content = content;

        // ------------------------------------------------------------------
        // Barre d'état
        // ------------------------------------------------------------------

        public void SetStatus(string message) => StatusMessage.Text = message;

        public void SetCursorPosition(int line, int column) => CursorText.Text = $"Ln {line}, Col {column}";

        public void SetLanguage(string language) => LanguageText.Text = language;

        public void SetEncoding(string encoding) => EncodingText.Text = encoding;

        public void SetBranch(string? branch)
        {
            BranchItem.Visibility = branch is null ? Visibility.Collapsed : Visibility.Visible;
            BranchText.Text = branch ?? string.Empty;
        }

        public void SetProblems(int errors, int warnings)
        {
            ErrorCountText.Text = errors.ToString();
            WarningCountText.Text = warnings.ToString();
        }

        public void SetIndentation(string text) => IndentText.Text = text;

        public void SetLineEnding(string ending) => LineEndingText.Text = ending;       

        // ------------------------------------------------------------------
        // Commandes
        // ------------------------------------------------------------------

        private void OnCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {

            if (e.Command == AppCommands.ShowExtensions)
            {
                ShowSidebarView(SidebarView.Extensions);
                return;
            }

            if (e.Command == AppCommands.ShowSettings)
            {
                ShowSidebarView(SidebarView.Settings);
                return;
            }

            // Les commandes d'affichage et de fenêtre sont gérées ici
            if (e.Command == AppCommands.ToggleExplorer)
            {
                ToggleExplorer();
                return;
            }

            if (e.Command == AppCommands.ToggleBottomPanel)
            {
                ToggleBottomPanel();
                return;
            }

            if (e.Command == AppCommands.SetTheme)
            {
                if (Enum.TryParse<AppTheme>(e.Parameter as string, out var theme))
                {
                    ThemeManager.Apply(theme);
                    SetStatus($"Thème appliqué : {theme}");
                }
                return;
            }

            if (e.Command == AppCommands.Exit)
            {
                Close();
                return;
            }

            if (e.Command == AppCommands.About)
            {
                new AboutWindow { Owner = this }.ShowDialog();
                return;
            }

            if (e.Command == AppCommands.ToggleRightPanel)
            {
                ToggleRightPanel();
                return;
            }

            // Le reste appartient aux autres modules
            if (CommandInvoked is null)
            {
                var name = (e.Command as RoutedUICommand)?.Text ?? "inconnue";
                SetStatus($"La commande « {name} » n'est pas encore branchée.");
                return;
            }

            CommandInvoked.Invoke(e.Command);
        }

        // Bouton en bas de la barre d'activité : passe de clair à sombre et inversement
        private void OnThemeButtonClick(object sender, RoutedEventArgs e)
        {
            ThemeManager.Apply(ThemeManager.IsDark ? AppTheme.Light : AppTheme.Dark);
            SetStatus(ThemeManager.IsDark ? "Thème sombre" : "Thème clair");
        }

                // Bouton « compte » : ouvre la fenêtre de connexion GitHub
        private void OnAccountButtonClick(object sender, RoutedEventArgs e)
        {
            new AccountWindow { Owner = this }.ShowDialog();
            UpdateAccountButton();
        }

        // Le bouton indique qui est connecté
        private void UpdateAccountButton()
        {
            var account = GitHubAuthService.LoadSaved();
            AccountButton.ToolTip = account is null
                ? "Se connecter (GitHub)"
                : $"Connecté : {account.Login}";
        }

        // ------------------------------------------------------------------
        // Affichage des panneaux
        // ------------------------------------------------------------------

        private enum SidebarView
        {
            Explorer,
            Extensions,
            Settings
        }

        private SidebarView _currentView = SidebarView.Explorer;

        private void OnExplorerButtonClick(object sender, RoutedEventArgs e) => ShowSidebarView(SidebarView.Explorer);

        private void OnExtensionsButtonClick(object sender, RoutedEventArgs e) => ShowSidebarView(SidebarView.Extensions);

        private void OnSettingsButtonClick(object sender, RoutedEventArgs e) => ShowSidebarView(SidebarView.Settings);

        //private void OnSettingsButtonClick(object sender, RoutedEventArgs e) => ShowSidebarView(SidebarView.Settings);

        private void ShowSidebarView(SidebarView view)
        {
            bool panelVisible = ExplorerPanel.Visibility == Visibility.Visible;

            if (panelVisible && _currentView == view)
            {
                ToggleExplorer();
                return;
            }

            _currentView = view;
            ExplorerView.Visibility = view == SidebarView.Explorer ? Visibility.Visible : Visibility.Collapsed;
            ExtensionsPanelView.Visibility = view == SidebarView.Extensions ? Visibility.Visible : Visibility.Collapsed;
            SettingsPanelView.Visibility = view == SidebarView.Settings ? Visibility.Visible : Visibility.Collapsed;

            if (!panelVisible)
            {
                ToggleExplorer();
            }
            else
            {
                UpdateIndicators();
            }
        }

        private void UpdateIndicators()
        {
            bool visible = ExplorerPanel.Visibility == Visibility.Visible;
            ExplorerIndicator.Visibility = visible && _currentView == SidebarView.Explorer ? Visibility.Visible : Visibility.Hidden;
            ExtensionsIndicator.Visibility = visible && _currentView == SidebarView.Extensions ? Visibility.Visible : Visibility.Hidden;
            SettingsIndicator.Visibility = visible && _currentView == SidebarView.Settings ? Visibility.Visible : Visibility.Hidden;
        }

        private void ToggleExplorer()
        {
            bool isVisible = ExplorerPanel.Visibility == Visibility.Visible;
        
            if (isVisible)
            {
                // On retient la largeur actuelle avant de replier la colonne
                _explorerWidth = ExplorerColumn.ActualWidth;
                ExplorerPanel.Visibility = Visibility.Collapsed;
                ExplorerSplitter.Visibility = Visibility.Collapsed;
                ExplorerColumn.MinWidth = 0;
                ExplorerColumn.Width = new GridLength(0);
            }
            else
            {
                ExplorerPanel.Visibility = Visibility.Visible;
                ExplorerSplitter.Visibility = Visibility.Visible;
                ExplorerColumn.MinWidth = 150;
                ExplorerColumn.Width = new GridLength(_explorerWidth);
            }
        
            UpdateIndicators();
        }

        private void ToggleBottomPanel()
        {
            bool isVisible = BottomPanel.Visibility == Visibility.Visible;

            if (isVisible)
            {
                _bottomPanelHeight = BottomPanelRow.ActualHeight;
                BottomPanel.Visibility = Visibility.Collapsed;
                BottomSplitter.Visibility = Visibility.Collapsed;
                BottomPanelRow.MinHeight = 0;
                BottomPanelRow.Height = new GridLength(0);
            }
            else
            {
                BottomPanel.Visibility = Visibility.Visible;
                BottomSplitter.Visibility = Visibility.Visible;
                BottomPanelRow.MinHeight = 80;
                BottomPanelRow.Height = new GridLength(_bottomPanelHeight);
            }
        }

                // Menu « Ouvrir un récent » : reconstruit à chaque ouverture du sous-menu
        private void OnRecentMenuOpened(object sender, RoutedEventArgs e)
        {
            if (!ReferenceEquals(e.OriginalSource, RecentMenu)) return;

            RecentMenu.Items.Clear();
            var recents = Router.Recents;
            var folders = recents?.GetRecentFolders().ToList() ?? new List<string>();
            var files = recents?.GetRecentFiles().ToList() ?? new List<string>();

            if (folders.Count == 0 && files.Count == 0)
            {
                RecentMenu.Items.Add(new MenuItem { Header = "(aucun élément récent)", IsEnabled = false });
                return;
            }

            foreach (var folder in folders) RecentMenu.Items.Add(CreateRecentItem(folder));
            if (folders.Count > 0 && files.Count > 0) RecentMenu.Items.Add(new Separator());
            foreach (var file in files) RecentMenu.Items.Add(CreateRecentItem(file));

            RecentMenu.Items.Add(new Separator());
            var clear = new MenuItem { Header = "Effacer les récents" };
            clear.Click += (_, _) => Router.ClearRecents();
            RecentMenu.Items.Add(clear);
        }

        private MenuItem CreateRecentItem(string path)
        {
            // Le « _ » est une touche d'accès dans un menu : on le double pour l'afficher
            var item = new MenuItem { Header = path.Replace("_", "__"), ToolTip = path };
            item.Click += (_, _) => OpenRecent(path);
            return item;
        }

        // Un récent peut être un dossier ou un fichier
        private async void OpenRecent(string path)
        {
            try
            {
                if (Directory.Exists(path)) Router.OpenFolderPath(path);
                else await Router.OpenPathAsync(path);
            }
            catch (Exception ex)
            {
                SetStatus($"Erreur : {ex.Message}");
            }
        }

        private void ToggleRightPanel()
        {
            bool isVisible = RightPanel.Visibility == Visibility.Visible;

            if (isVisible)
            {
                _rightPanelWidth = RightColumn.ActualWidth;
                RightPanel.Visibility = Visibility.Collapsed;
                RightSplitter.Visibility = Visibility.Collapsed;
                RightColumn.MinWidth = 0;
                RightColumn.Width = new GridLength(0);
            }
            else
            {
                RightPanel.Visibility = Visibility.Visible;
                RightSplitter.Visibility = Visibility.Visible;
                RightColumn.MinWidth = 200;
                RightColumn.Width = new GridLength(_rightPanelWidth);
            }
        }

    }
}