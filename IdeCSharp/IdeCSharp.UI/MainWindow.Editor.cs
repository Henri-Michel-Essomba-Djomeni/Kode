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
        _editorService = _editorView.Service;   // le service est créé par EditorView lui-même   // ★ ligne à confirmer avec le résultat de l'étape 0

        // Le contrôle des onglets prend la place de l'écran d'accueil
        SetEditorContent(_editorView);
        Router.Editor = _editorService;

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

        // Changement d'onglet : langage dans la barre d'état + aperçu du fichier.
        // On attend la fin de OpenDocument (Background), sinon le document
        // n'est pas encore enregistré dans le service et GetText échouerait.
        _editorService.ActiveDocumentChanged += path =>
            Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
            {
                try
                {
                    SetLanguage(_editorService.ActiveLanguageName);
                    EditorOverview.SetText(path is null ? string.Empty : _editorService.GetText(path));
                }
                catch (InvalidOperationException)
                {
                    // Onglet fermé entre-temps : rien à afficher
                    EditorOverview.SetText(string.Empty);
                }
            });

        // Clic dans l'aperçu : on place le curseur dans le fichier actif
        EditorOverview.LineRequested += line =>
        {
            var path = _editorService.ActiveDocumentPath;
            if (path is not null) _editorService.GoToLine(path, line);
        };
    }
}