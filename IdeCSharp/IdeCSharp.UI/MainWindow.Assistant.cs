using IdeCSharp.Core.Assistant;

namespace IdeCSharp.UI;

// Assistant IA du panneau de droite.
partial class MainWindow
{
    private void InitAssistant()
    {
        AssistantPane.Service = new GeminiAssistantService();
        AssistantPane.KeyRequested += OnAssistantKeyRequested;
    }

    private void OnAssistantKeyRequested()
    {
        new AssistantKeyWindow { Owner = this }.ShowDialog();
        AssistantPane.RefreshState();
    }
}