using System.IO;
using System.Windows.Threading;
using IdeCSharp.UI.Editor;

namespace IdeCSharp.UI;

// Branchement du module Éditeur (Valdez) : service + onglets dans la zone centrale.
// Fichier à part, sur le même modèle que MainWindow.Execution.cs.
partial class MainWindow
{
    private EditorView? _editorView;
    private EditorService? _editorService;

    private void InitEditorModule()
    {
        _editorView = new EditorView();
        _editorService = _editorView.Service;   // le service est créé par EditorView lui-même

        // Le contrôle des onglets prend la place de l'écran d'accueil
        SetEditorContent(_editorView);
        Router.Editor = _editorService;

        // Le routeur doit connaître le fichier actif (Exécuter, Enregistrer, assistant...)
        Router.GetActivePath = () => _editorService.ActiveDocumentPath;

        // Après chaque enregistrement fait par le routeur, l'onglet perd son point « modifié »
        Router.DocumentSaved = path => _editorService.MarkAsSaved(path);

        // Enregistrement quand on ferme un onglet modifié et qu'on répond « Oui »
        _editorService.SaveHandler = path =>
        {
            try
            {
                File.WriteAllText(path, _editorService.GetText(path));
                return true;
            }
            catch (Exception ex)
            {
                SetStatus($"Échec de l'enregistrement : {ex.Message}");
                return false;
            }
        };

        // Barre d'état : position du curseur
        _editorService.CaretPositionChanged += SetCursorPosition;

        // Changement d'onglet : langage, encodage et aperçu du fichier.
        // On attend la fin de OpenDocument (Background), sinon le document
        // n'est pas encore enregistré dans le service et GetText échouerait.
        _editorService.ActiveDocumentChanged += path =>
            Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
            {
                try
                {
                    SetLanguage(_editorService.ActiveLanguageName);
                    SetEncoding(Router.EncodingFor(path));
                    EditorOverview.SetText(path is null ? string.Empty : _editorService.GetText(path));
                }
                catch (InvalidOperationException)
                {
                    // Onglet fermé entre-temps : rien à afficher
                    EditorOverview.SetText(string.Empty);
                }
            });

        // Accueil : liste des récents (un clic ouvre) et mise à jour quand elle change
        _editorView.Welcome.RecentRequested += OpenRecent;
        Router.RecentsChanged += RefreshWelcomeRecents;
        RefreshWelcomeRecents();

        // Double-clic sur une erreur dans SORTIE : ouvre le fichier à la bonne ligne
        if (_outputPanel is not null)
            _outputPanel.ErrorActivated += OnErrorActivated;
    }

    // Remet à jour la liste des récents de l'accueil (dossiers d'abord, puis fichiers)
    private void RefreshWelcomeRecents()
    {
        var recents = Router.Recents;
        if (recents is null || _editorView is null) return;

        _editorView.Welcome.SetRecents(recents.GetRecentFolders().Concat(recents.GetRecentFiles()));
    }

    // Erreur de compilation cliquée dans SORTIE : on ouvre le fichier puis on place le curseur
    private async void OnErrorActivated(string path, int line, int column)
    {
        try
        {
            await Router.OpenPathAsync(path);

            // On laisse l'éditeur finir de se mettre en place avant de placer le curseur
            _ = Dispatcher.BeginInvoke(DispatcherPriority.Background,
                () => _editorService?.GoToLine(path, line, column));
        }
        catch (Exception ex)
        {
            SetStatus($"Erreur : {ex.Message}");
        }
    }
}