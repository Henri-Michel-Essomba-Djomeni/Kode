using System.IO;
using System.Windows;
using System.Windows.Input;
using IdeCSharp.Core.Interfaces;
using Microsoft.Win32;
using IdeCSharp.Core.Git;

namespace IdeCSharp.UI
{
    /// <summary>
    /// Fait le lien entre les menus / raccourcis de la fenêtre et les modules.
    /// Chaque module s'y branche en renseignant sa propriété (Files, Editor, Execution).
    /// Tant qu'une propriété est vide, la commande l'annonce dans la barre d'état.
    /// </summary>
    public class CommandRouter
    {
        private const string CSharpFilter = "Fichiers C# (*.cs)|*.cs|Tous les fichiers (*.*)|*.*";

        private readonly MainWindow _window;
        private CancellationTokenSource? _runCts;

        // Les modules à brancher (affectés au moment de l'assemblage)
        public IFileService? Files { get; set; }
        public IEditorService? Editor { get; set; }
        public IExecutionService? Execution { get; set; }

        /// <summary>Donne le chemin du document de l'onglet actif (fourni par le module Éditeur).</summary>
        public Func<string?> GetActivePath { get; set; } = () => null;

        /// <summary>Dossier de projet actuellement ouvert.</summary>
        public string? CurrentFolder { get; private set; }

        /// <summary>Levé quand l'utilisateur choisit un dossier : l'explorateur s'y abonne.</summary>
        public event Action<string>? FolderOpened;

        public CommandRouter(MainWindow window)
        {
            _window = window;
            _window.CommandInvoked += OnCommandInvoked;

            // Un clic sur l'aperçu du fichier : l'éditeur va à la ligne demandée
            _window.EditorOverview.LineRequested += line =>
            {
                var path = GetActivePath();
                if (path != null) Editor?.GoToLine(path, line);
            };

            // L'assistant reçoit le fichier ouvert comme contexte
            _window.AssistantPanel.GetContext = () =>
            {
                var path = GetActivePath();
                var editor = Editor;
                return path is null || editor is null
                    ? (null, null)
                    : (Path.GetFileName(path), editor.GetText(path));
            };
        }

        // async void est acceptable ici car c'est un gestionnaire d'événement ;
        // le try/catch évite qu'une exception ne ferme toute l'application.
        private async void OnCommandInvoked(ICommand command)
        {
            try
            {
                await HandleAsync(command);
            }
            catch (OperationCanceledException)
            {
                _window.SetStatus("Opération annulée.");
            }
            catch (Exception ex)
            {
                _window.SetStatus($"Erreur : {ex.Message}");
            }
        }

        private async Task HandleAsync(ICommand command)
        {
            if (command == ApplicationCommands.New) await NewFileAsync();
            else if (command == ApplicationCommands.Open) await OpenFileAsync();
            else if (command == AppCommands.OpenFolder) OpenFolder();
            else if (command == ApplicationCommands.Save) await SaveAsync();
            else if (command == AppCommands.SaveAs) await SaveAsAsync();
            else if (command == AppCommands.Run) await RunAsync();
            else if (command == AppCommands.Stop) Stop();
            else if (command == ApplicationCommands.Find
                  || command == ApplicationCommands.Replace
                  || command == AppCommands.GoToLine)
            {
                // Ces commandes ouvrent des barres propres à l'éditeur : c'est le module
                // de Valdez qui les écoute. Ici on signale seulement qu'il manque.
                if (Editor is null) Missing("Éditeur");
            }
        }

        private void Missing(string module) =>
            _window.SetStatus($"Le module {module} n'est pas encore branché.");

        // ------------------------------------------------------------------
        // Fichier
        // ------------------------------------------------------------------

        private Task NewFileAsync()
        {
            var files = Files;
            var editor = Editor;
            if (files is null) { Missing("Fichiers"); return Task.CompletedTask; }
            if (editor is null) { Missing("Éditeur"); return Task.CompletedTask; }

            var dialog = new SaveFileDialog { Title = "Nouveau fichier", Filter = CSharpFilter, DefaultExt = ".cs" };
            if (dialog.ShowDialog(_window) != true) return Task.CompletedTask;

            files.CreateFile(dialog.FileName);
            editor.OpenDocument(dialog.FileName, string.Empty);
            _window.SetStatus($"Créé : {Path.GetFileName(dialog.FileName)}");
            return Task.CompletedTask;
        }

        private async Task OpenFileAsync()
        {
            if (Files is null) { Missing("Fichiers"); return; }
            if (Editor is null) { Missing("Éditeur"); return; }

            var dialog = new OpenFileDialog { Title = "Ouvrir un fichier", Filter = CSharpFilter };
            if (dialog.ShowDialog(_window) != true) return;

            await OpenPathAsync(dialog.FileName);
        }

        /// <summary>Ouvre un fichier dans l'éditeur. Public : l'explorateur l'appellera au double-clic.</summary>
        public async Task OpenPathAsync(string path)
        {
            var files = Files;
            var editor = Editor;
            if (files is null) { Missing("Fichiers"); return; }
            if (editor is null) { Missing("Éditeur"); return; }

            // Progress<T> met à jour la barre d'état depuis le bon thread
            var progress = new Progress<double>(p => _window.SetStatus($"Chargement… {p:P0}"));
            var content = await files.ReadFileAsync(path, progress);

            editor.OpenDocument(path, content);
            _window.EditorOverview.SetText(content);
            _window.SetLanguage(LanguageFor(path));
            _window.SetStatus($"Ouvert : {Path.GetFileName(path)}");
        }

        private void OpenFolder()
        {
            var dialog = new OpenFolderDialog { Title = "Ouvrir un dossier de projet" };
            if (dialog.ShowDialog(_window) != true) return;

            CurrentFolder = dialog.FolderName;
            FolderOpened?.Invoke(CurrentFolder);
            _window.SetBranch(GitInfo.GetBranch(CurrentFolder));
            _window.SetStatus($"Dossier ouvert : {CurrentFolder}");
        }

        private async Task SaveAsync()
        {
            var files = Files;
            var editor = Editor;
            if (files is null) { Missing("Fichiers"); return; }
            if (editor is null) { Missing("Éditeur"); return; }

            var path = GetActivePath();
            if (path is null)
            {
                _window.SetStatus("Aucun document à enregistrer.");
                return;
            }

            await files.SaveFileAsync(path, editor.GetText(path));
            _window.SetStatus($"Enregistré : {Path.GetFileName(path)}");
        }

        private async Task SaveAsAsync()
        {
            var files = Files;
            var editor = Editor;
            if (files is null) { Missing("Fichiers"); return; }
            if (editor is null) { Missing("Éditeur"); return; }

            var path = GetActivePath();
            if (path is null)
            {
                _window.SetStatus("Aucun document à enregistrer.");
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Enregistrer sous",
                Filter = CSharpFilter,
                FileName = Path.GetFileName(path)
            };
            if (dialog.ShowDialog(_window) != true) return;

            var text = editor.GetText(path);
            await files.SaveFileAsync(dialog.FileName, text);

            // La copie s'ouvre dans son propre onglet ; l'ancien onglet reste tel quel
            editor.OpenDocument(dialog.FileName, text);
            _window.SetStatus($"Enregistré sous : {Path.GetFileName(dialog.FileName)}");
        }

        // ------------------------------------------------------------------
        // Exécuter
        // ------------------------------------------------------------------

        private async Task RunAsync()
        {
            var execution = Execution;
            var files = Files;
            var editor = Editor;
            if (execution is null) { Missing("Exécution"); return; }
            if (files is null) { Missing("Fichiers"); return; }
            if (editor is null) { Missing("Éditeur"); return; }

            if (_runCts is not null)
            {
                _window.SetStatus("Un programme est déjà en cours. Arrête-le d'abord (Maj+F5).");
                return;
            }

            if (!execution.IsSdkAvailable())
            {
                MessageBox.Show(_window,
                    "Le SDK .NET est introuvable sur cette machine.\n" +
                    "Installe-le depuis https://dotnet.microsoft.com/download puis relance Kode.",
                    "SDK .NET manquant", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (CurrentFolder is null)
            {
                _window.SetStatus("Ouvre d'abord un dossier de projet (Ctrl+Maj+O).");
                return;
            }

            _runCts = new CancellationTokenSource();
            try
            {
                // On enregistre tout ce qui est modifié pour compiler la dernière version
                foreach (var path in editor.GetModifiedDocuments())
                {
                    await files.SaveFileAsync(path, editor.GetText(path));
                }

                _window.SetStatus("Compilation en cours…");
                var result = await execution.BuildAsync(CurrentFolder, _runCts.Token);
                if (!result.Success)
                {
                    _window.SetStatus($"Échec de la compilation : {result.Errors.Count} erreur(s).");
                    return;
                }

                _window.SetStatus("Exécution en cours…");
                await execution.RunAsync(CurrentFolder, _runCts.Token);
                _window.SetStatus("Programme terminé.");
            }
            finally
            {
                _runCts.Dispose();
                _runCts = null;
            }
        }

        private void Stop()
        {
            if (Execution is null) { Missing("Exécution"); return; }

            _runCts?.Cancel();
            Execution.Stop();
            _window.SetStatus("Arrêt demandé.");
        }

        // Nom du langage affiché dans la barre d'état, d'après l'extension
        private static string LanguageFor(string path) =>
            Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".cs" => "C#",
                ".json" => "JSON",
                ".xml" or ".xaml" => "XML",
                ".py" => "Python",
                ".html" => "HTML",
                _ => "Texte brut"
            };
    }
}