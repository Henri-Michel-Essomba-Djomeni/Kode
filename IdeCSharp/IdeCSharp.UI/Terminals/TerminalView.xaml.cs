using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using IdeCSharp.Core.Interfaces;
using IdeCSharp.Core.Models;

namespace IdeCSharp.UI.Terminal
{
    /// <summary>Console cmd / PowerShell affichée dans le panneau du bas.</summary>
    public partial class TerminalView : UserControl
    {
        // On garde l'affichage raisonnable : au-delà, on coupe le début.
        private const int MaxChars = 200_000;

        private ITerminalService? _terminal;
        private Func<string?> _getWorkingDirectory = () => null;
        private readonly List<string> _history = new();
        private int _historyIndex;

        public TerminalView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Branche le service et indique comment connaître le dossier de travail
        /// (par exemple () => router.CurrentFolder).
        /// </summary>
        public void Attach(ITerminalService terminal, Func<string?> getWorkingDirectory)
        {
            _terminal = terminal;
            _getWorkingDirectory = getWorkingDirectory;

            // Ces événements arrivent d'un thread d'arrière-plan : on repasse par le Dispatcher.
            terminal.OutputReceived += text => Dispatcher.InvokeAsync(() => Append(text));
            terminal.Exited += code => Dispatcher.InvokeAsync(
                () => Append($"\r\n[Console terminée, code {code}. Clique sur « Redémarrer ».]\r\n"));
        }

        /// <summary>Démarre la console si ce n'est pas déjà fait (appelé à l'ouverture de l'onglet).</summary>
        public void EnsureStarted()
        {
            if (_terminal is not null && !_terminal.IsRunning) StartShell();
            InputBox.Focus();
        }

        private ShellKind SelectedShell =>
            ShellCombo.SelectedIndex == 1 ? ShellKind.PowerShell : ShellKind.Cmd;

        private void StartShell()
        {
            if (_terminal is null) return;

            var folder = _getWorkingDirectory()
                         ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            OutputBox.Clear();
            try
            {
                _terminal.Start(SelectedShell, folder);
            }
            catch (Exception ex)
            {
                Append($"Impossible de démarrer la console : {ex.Message}\r\n");
            }
        }

        private void Append(string text)
        {
            OutputBox.AppendText(text);
            if (OutputBox.Text.Length > MaxChars)
            {
                OutputBox.Text = OutputBox.Text[^(MaxChars / 2)..];
            }
            OutputBox.ScrollToEnd();
        }

        // ------------------------------------------------------------------
        // Saisie : Entrée envoie, flèches haut / bas parcourent l'historique
        // ------------------------------------------------------------------

        private void OnInputKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                SubmitLine();
            }
            else if (e.Key == Key.Up && _history.Count > 0)
            {
                e.Handled = true;
                _historyIndex = Math.Max(0, _historyIndex - 1);
                ShowHistory();
            }
            else if (e.Key == Key.Down && _history.Count > 0)
            {
                e.Handled = true;
                _historyIndex = Math.Min(_history.Count, _historyIndex + 1);
                ShowHistory();
            }
        }

        private void SubmitLine()
        {
            if (_terminal is null) return;

            var line = InputBox.Text;
            InputBox.Clear();

            if (!_terminal.IsRunning) StartShell();
            if (!_terminal.IsRunning) return; // le démarrage a échoué, le message est affiché

            if (!string.IsNullOrWhiteSpace(line)) _history.Add(line);
            _historyIndex = _history.Count;

            // PowerShell ne réécrit pas la commande tapée quand son entrée est redirigée :
            // on l'affiche nous-mêmes. (À vérifier pour cmd : voir la checklist de tests.)
            if (SelectedShell == ShellKind.PowerShell) Append($"PS> {line}\r\n");

            _terminal.SendCommand(line);
        }

        private void ShowHistory()
        {
            InputBox.Text = _historyIndex < _history.Count ? _history[_historyIndex] : string.Empty;
            InputBox.CaretIndex = InputBox.Text.Length;
        }

        // ------------------------------------------------------------------
        // Barre du haut
        // ------------------------------------------------------------------

        private void OnShellChanged(object sender, SelectionChangedEventArgs e)
        {
            // Au tout premier affichage, le service n'est pas encore branché : on ne fait rien.
            if (_terminal is null || !IsLoaded) return;
            StartShell();
        }

        private void OnRestartClick(object sender, RoutedEventArgs e) => StartShell();

        private void OnClearClick(object sender, RoutedEventArgs e) => OutputBox.Clear();
    }
}
