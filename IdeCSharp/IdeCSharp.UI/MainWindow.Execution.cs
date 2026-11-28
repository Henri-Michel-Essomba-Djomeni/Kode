using System.IO;
using IdeCSharp.Core.Services;

namespace IdeCSharp.UI;

// Branchement du module Exécution (Kammadeu) : service + panneau Sortie.
// Fichier à part, pour ne pas toucher au code des autres modules.
partial class MainWindow
{
    private ExecutionService? _executionService;
    private OutputPanel? _outputPanel;

    private void InitExecutionModule()
    {
        _executionService = new ExecutionService();
        _outputPanel = new OutputPanel();
        _outputPanel.Bind(_executionService);
        _outputPanel.ErrorActivated += OnErrorActivated;

        _bottomTabs.OutputHost.Content = _outputPanel;   // le panneau va dans l'onglet SORTIE
        Router.Execution = _executionService;    // le routeur peut maintenant lancer et arrêter

        // TEST TEMPORAIRE : décommente la ligne suivante pour vérifier sans attendre les autres modules.
        // _ = _executionService.RunAsync(@"C:\dev\IDE-tests\Hello");
    }
    // Double-clic sur une erreur du panneau Sortie : ouvre le fichier et place le curseur (T44).
    /*private void OnErrorActivated(string path, int line, int column)
    {
        var editor = Router.Editor;
        if (editor == null) return;   // l'éditeur n'est pas encore branché

        try
        {
            editor.OpenDocument(path, File.ReadAllText(path));
            editor.GoToLine(path, line, column);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }*/ 
    
}