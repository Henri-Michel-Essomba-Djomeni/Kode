using System.Windows;
using System.Windows.Controls;

namespace IdeCSharp.UI
{
    /// <summary>
    /// Contenu du panneau du bas, partagé sans conflit entre deux modules :
    /// l'Exécution place son affichage dans SetOutputContent, le Terminal vit dans TerminalPane.
    /// </summary>
    public partial class BottomPanelView : UserControl
    {
        public BottomPanelView()
        {
            InitializeComponent();
        }

        /// <summary>Place l'affichage de la sortie des programmes dans l'onglet SORTIE.</summary>
        public void SetOutputContent(UIElement content) => OutputHost.Content = content;

        /// <summary>Affiche l'onglet SORTIE (à appeler au lancement d'un programme).</summary>
        public void ShowOutput() => Tabs.SelectedIndex = 0;

        private void OnTabChanged(object sender, SelectionChangedEventArgs e)
        {
            // Les ComboBox et autres listes à l'intérieur des onglets envoient aussi
            // SelectionChanged, qui « remonte » jusqu'ici. On ne réagit qu'aux changements d'onglet.
            if (!ReferenceEquals(e.OriginalSource, Tabs)) return;

            if (ReferenceEquals(Tabs.SelectedItem, TerminalTab))
            {
                TerminalPane.EnsureStarted();
            }
        }
    }
}
