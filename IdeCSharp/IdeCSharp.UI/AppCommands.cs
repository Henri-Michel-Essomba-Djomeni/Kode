using System.Windows.Input;

namespace IdeCSharp
{
    /// <summary>
    /// Toutes les commandes de l'IDE au même endroit : menus et raccourcis clavier
    /// pointent ici, et chaque module s'y branche sans toucher à la fenêtre.
    /// Les commandes standard (Nouveau, Ouvrir, Enregistrer, Rechercher, Remplacer)
    /// viennent directement de ApplicationCommands.
    /// </summary>
    public static class AppCommands
    {
        public static readonly RoutedUICommand OpenFolder = Create("Ouvrir un dossier...", nameof(OpenFolder),
            new KeyGesture(Key.O, ModifierKeys.Control | ModifierKeys.Shift));

        public static readonly RoutedUICommand SaveAs = Create("Enregistrer sous...", nameof(SaveAs),
            new KeyGesture(Key.S, ModifierKeys.Control | ModifierKeys.Shift));

        public static readonly RoutedUICommand GoToLine = Create("Aller à la ligne...", nameof(GoToLine),
            new KeyGesture(Key.G, ModifierKeys.Control));

        public static readonly RoutedUICommand Run = Create("Exécuter", nameof(Run),
            new KeyGesture(Key.F5));

        public static readonly RoutedUICommand Stop = Create("Arrêter", nameof(Stop),
            new KeyGesture(Key.F5, ModifierKeys.Shift));

        public static readonly RoutedUICommand ToggleExplorer = Create("Explorateur", nameof(ToggleExplorer),
            new KeyGesture(Key.B, ModifierKeys.Control));

        public static readonly RoutedUICommand ToggleBottomPanel = Create("Panneau du bas", nameof(ToggleBottomPanel),
            new KeyGesture(Key.J, ModifierKeys.Control));

        public static readonly RoutedUICommand About = Create("À propos de Kode", nameof(About));

        public static readonly RoutedUICommand Exit = Create("Quitter", nameof(Exit));

        private static RoutedUICommand Create(string text, string name, params InputGesture[] gestures)
        {
            var gestureCollection = new InputGestureCollection();
            foreach (var gesture in gestures)
            {
                gestureCollection.Add(gesture);
            }
            return new RoutedUICommand(text, name, typeof(AppCommands), gestureCollection);
        }
    }
}