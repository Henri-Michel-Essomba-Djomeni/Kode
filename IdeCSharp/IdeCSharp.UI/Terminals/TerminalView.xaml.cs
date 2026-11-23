using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
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

        // Invite de commandes : « C:\dossier> », « PS C:\dossier> » ou « PS> »
        private static readonly Regex PromptRegex =
            new(@"^(?:PS\s+)?[A-Za-z]:\\[^>\n]*>|^PS>", RegexOptions.Compiled);

        // Lignes qui ressemblent à une erreur : affichées en rouge
        private static readonly Regex ErrorRegex =
            new(@"error|erreur|exception|traceback|fatal|n'est pas reconnu|not recognized|introuvable",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private ITerminalService? _terminal;
        private Func<string?> _getWorkingDirectory = () => null;
        private readonly List<string> _history = new();
        private int _historyIndex;

        // Le texte de la console est un seul paragraphe fait de morceaux colorés
        private readonly Paragraph _paragraph = new() { Margin = new Thickness(0) };
        private Run? _lastPlain;     // dernier morceau de texte normal, pour y accoler la suite
        private int _length;         // nombre de caractères affichés

        public TerminalView()
        {
            InitializeComponent();
            OutputBox.Document = new FlowDocument(_paragraph) { PagePadding = new Thickness(0) };
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

        /// <summary>
        /// Appelé quand l'utilisateur ouvre un dossier : si la console tourne déjà,
        /// on la déplace dans ce dossier. Sinon elle démarrera déjà au bon endroit.
        /// </summary>
        public void OnFolderOpened(string folder)
        {
            if (_terminal is null || !_terminal.IsRunning) return;

            string command = SelectedShell == ShellKind.PowerShell
                ? $"Set-Location -LiteralPath '{folder.Replace("'", "''")}'"
                : $"cd /d \"{folder}\"";
            _terminal.SendCommand(command);
        }

        private ShellKind SelectedShell =>
            ShellCombo.SelectedIndex == 1 ? ShellKind.PowerShell : ShellKind.Cmd;

        private void StartShell()
        {
            if (_terminal is null) return;

            var folder = _getWorkingDirectory()
                         ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            ClearOutput();
            try
            {
                _terminal.Start(SelectedShell, folder);
            }
            catch (Exception ex)
            {
                Append($"Impossible de démarrer la console : {ex.Message}\r\n");
            }
        }

        // ------------------------------------------------------------------
        // Affichage coloré
        // ------------------------------------------------------------------

        private void ClearOutput()
        {
            _paragraph.Inlines.Clear();
            _lastPlain = null;
            _length = 0;
        }

        private void Append(string text)
        {
            // On unifie les fins de ligne : un seul saut de ligne « \n »
            text = text.Replace("\r\n", "\n").Replace("\r", string.Empty);

            var lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                {
                    _paragraph.Inlines.Add(new LineBreak());
                    _length++;
                    _lastPlain = null;
                }

                if (lines[i].Length > 0) AddSegment(lines[i]);
            }

            TrimStart();
            OutputBox.ScrollToEnd();
        }

        // Choisit la couleur d'un morceau de ligne
        private void AddSegment(string segment)
        {
            _length += segment.Length;

            // Invite : seule la partie « C:\dossier> » est en couleur d'accent
            var prompt = PromptRegex.Match(segment);
            if (prompt.Success)
            {
                AddRun(prompt.Value, "TerminalPromptBrush");
                if (segment.Length > prompt.Length) AddRun(segment[prompt.Length..], null);
                return;
            }

            AddRun(segment, ErrorRegex.IsMatch(segment) ? "TerminalErrorBrush" : null);
        }

        // brushKey = null : texte normal (couleur du terminal), sinon couleur du thème
        private void AddRun(string text, string? brushKey)
        {
            // Le texte normal s'ajoute au morceau précédent : moins d'éléments à afficher
            if (brushKey is null && _lastPlain is not null
                && ReferenceEquals(_paragraph.Inlines.LastInline, _lastPlain))
            {
                _lastPlain.Text += text;
                return;
            }

            var run = new Run(text);
            if (brushKey is not null)
            {
                // SetResourceReference : la couleur suit le thème quand on le change
                run.SetResourceReference(TextElement.ForegroundProperty, brushKey);
                _lastPlain = null;
            }
            else
            {
                _lastPlain = run;
            }

            _paragraph.Inlines.Add(run);
        }

        // Au-delà de la limite, on retire le début de l'affichage
        private void TrimStart()
        {
            while (_length > MaxChars && _paragraph.Inlines.FirstInline is { } first)
            {
                _length -= first is Run run ? run.Text.Length : 1;
                if (ReferenceEquals(first, _lastPlain)) _lastPlain = null;
                _paragraph.Inlines.Remove(first);
            }
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

        private void OnClearClick(object sender, RoutedEventArgs e) => ClearOutput();
    }
}