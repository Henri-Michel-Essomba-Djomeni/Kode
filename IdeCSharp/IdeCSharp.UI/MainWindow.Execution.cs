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

        BottomHost.Content = _outputPanel;       // le panneau remplace le texte provisoire
        Router.Execution = _executionService;    // le routeur peut maintenant lancer et arrêter

        // TEST TEMPORAIRE : décommente la ligne suivante pour vérifier sans attendre les autres modules.
        // _ = _executionService.RunAsync(@"C:\dev\IDE-tests\Hello");
    }
}