using System;
using System.Windows;
using System.Windows.Input;

namespace IdeCSharp.UI
{
    public partial class MainWindow : Window
    {
        // Tailles mémorisées pour retrouver la mise en page après un masquage
        private double _explorerWidth = 260;
        private double _bottomPanelHeight = 200;

        /// <summary>
        /// Levé pour chaque commande non gérée par la fenêtre elle-même
        /// (Nouveau, Ouvrir, Enregistrer, Exécuter...). Les modules s'abonnent ici.
        /// </summary>
        public event Action<ICommand>? CommandInvoked;

        public MainWindow()
        {
            InitializeComponent();
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

        // ------------------------------------------------------------------
        // Commandes
        // ------------------------------------------------------------------

        private void OnCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
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

            // Le reste appartient aux autres modules
            if (CommandInvoked is null)
            {
                var name = (e.Command as RoutedUICommand)?.Text ?? "inconnue";
                SetStatus($"La commande « {name} » n'est pas encore branchée.");
                return;
            }

            CommandInvoked.Invoke(e.Command);
        }

        // ------------------------------------------------------------------
        // Affichage des panneaux
        // ------------------------------------------------------------------

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
    }
}
