using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using IdeCSharp.Core.Assistant;

namespace IdeCSharp.UI
{
    public partial class AssistantView : UserControl
    {
        private readonly ObservableCollection<ChatMessage> _messages = new();
        private CancellationTokenSource? _cts;
        private IAssistantService? _service;

        /// <summary>Le service d'IA. Tant qu'il est vide, l'assistant l'indique et ne répond pas.</summary>
        public IAssistantService? Service
        {
            get => _service;
            set
            {
                _service = value;
                UpdateState();
            }
        }

        /// <summary>Donne le nom et le contenu du fichier ouvert, pour que l'assistant connaisse le contexte.</summary>
        public Func<(string? Name, string? Content)> GetContext { get; set; } = () => (null, null);

        public AssistantView()
        {
            InitializeComponent();
            MessageList.ItemsSource = _messages;
            _messages.CollectionChanged += OnMessagesChanged;
            UpdateState();
        }

        private void UpdateState()
        {
            StateText.Text = _service is { IsConfigured: true } ? "· prêt" : "· non connecté";
        }

        private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            EmptyText.Visibility = _messages.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            Scroller.ScrollToEnd();
        }

        /// <summary>Levé quand l'utilisateur clique sur « Clé API ».</summary>
        public event Action? KeyRequested;

        private void OnKeyClick(object sender, RoutedEventArgs e) => KeyRequested?.Invoke();

        /// <summary>Met à jour « prêt / non connecté » (après un changement de clé).</summary>
        public void RefreshState() => UpdateState();

        private void OnClearClick(object sender, RoutedEventArgs e) => _messages.Clear();

        private async void OnSendClick(object sender, RoutedEventArgs e) => await SendAsync();

        private void OnInputKeyDown(object sender, KeyEventArgs e)
        {
            // Entrée envoie ; Maj+Entrée ajoute un retour à la ligne
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
            {
                e.Handled = true;
                _ = SendAsync();
            }
        }

        private async Task SendAsync()
        {
            var question = InputBox.Text.Trim();
            if (question.Length == 0 || _cts is not null) return;   // vide, ou une réponse est déjà en cours

            InputBox.Clear();
            _messages.Add(new ChatMessage(true, question));

            var service = _service;
            if (service is null || !service.IsConfigured)
            {
                _messages.Add(new ChatMessage(false,
                    "L'assistant n'est pas connecté : clique sur « Clé API » en haut du panneau pour ajouter ta clé Gemini gratuite."));
                return;
            }

            var reply = new ChatMessage(false, string.Empty);
            _messages.Add(reply);
            _cts = new CancellationTokenSource();
            SendButton.IsEnabled = false;

            try
            {
                var (name, content) = GetContext();
                await foreach (var chunk in service.AskAsync(question, name, content, _cts.Token))
                {
                    reply.Text += chunk;
                    Scroller.ScrollToEnd();
                }
            }
            catch (OperationCanceledException)
            {
                reply.Text += "\n[Interrompu]";
            }
            catch (Exception ex)
            {
                reply.Text += $"\n[Erreur : {ex.Message}]";
            }
            finally
            {
                _cts.Dispose();
                _cts = null;
                SendButton.IsEnabled = true;
            }
        }
    }
}