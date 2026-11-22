using System.ComponentModel;

namespace IdeCSharp.UI
{
    /// <summary>Un message de la conversation. Le texte peut grandir pendant que la réponse arrive.</summary>
    public class ChatMessage : INotifyPropertyChanged
    {
        private string _text;

        public ChatMessage(bool isUser, string text)
        {
            IsUser = isUser;
            _text = text;
        }

        public bool IsUser { get; }

        public string Text
        {
            get => _text;
            set
            {
                _text = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}