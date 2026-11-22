using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace IdeCSharp.UI.Editor;

/// <summary>
/// Barre compacte de recherche et de remplacement de texte dans l'éditeur.
/// </summary>
public partial class FindReplaceBar : UserControl
{
    private bool _isReadOnly;
    private bool _isLargeFileMode;

    /// <summary>
    /// Texte actuellement saisi dans la zone de recherche.
    /// </summary>
    public string SearchText
    {
        get => SearchBox.Text;
        set => SearchBox.Text = value;
    }

    /// <summary>
    /// Indique si la recherche doit respecter la casse.
    /// </summary>
    public bool MatchCase
    {
        get => MatchCaseButton.IsChecked == true;
        set => MatchCaseButton.IsChecked = value;
    }

    /// <summary>
    /// Texte actuellement saisi dans la zone de remplacement.
    /// </summary>
    public string ReplaceText
    {
        get => ReplaceBox.Text;
        set => ReplaceBox.Text = value;
    }

    /// <summary>
    /// Déclenché quand le texte ou l'option de casse de recherche change.
    /// </summary>
    public event EventHandler? SearchChanged;

    /// <summary>
    /// Déclenché pour naviguer vers l'occurrence suivante (true) ou précédente (false).
    /// </summary>
    public event Action<bool>? FindRequested;

    /// <summary>
    /// Déclenché pour remplacer l'occurrence courante.
    /// </summary>
    public event EventHandler? ReplaceRequested;

    /// <summary>
    /// Déclenché pour remplacer toutes les occurrences.
    /// </summary>
    public event EventHandler? ReplaceAllRequested;

    /// <summary>
    /// Déclenché pour fermer la barre de recherche.
    /// </summary>
    public event EventHandler? CloseRequested;

    public FindReplaceBar()
    {
        InitializeComponent();

        SearchBox.TextChanged += OnSearchBoxTextChanged;
        MatchCaseButton.Click += OnMatchCaseClick;

        PreviousButton.Click += (_, _) => FindRequested?.Invoke(false);
        NextButton.Click += (_, _) => FindRequested?.Invoke(true);
        ReplaceButton.Click += (_, _) => ReplaceRequested?.Invoke(this, EventArgs.Empty);
        ReplaceAllButton.Click += (_, _) => ReplaceAllRequested?.Invoke(this, EventArgs.Empty);
        CloseButton.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);

        SearchBox.PreviewKeyDown += OnSearchBoxPreviewKeyDown;
        ReplaceBox.PreviewKeyDown += OnReplaceBoxPreviewKeyDown;
        PreviewKeyDown += OnBarPreviewKeyDown;

        UpdateControlsState();
    }

    /// <summary>
    /// Affiche la barre, configure le mode et préremplit la recherche si applicable.
    /// </summary>
    public void Open(bool replaceMode, string? initialText)
    {
        Visibility = Visibility.Visible;
        ReplaceRow.Visibility = replaceMode ? Visibility.Visible : Visibility.Collapsed;

        if (!string.IsNullOrEmpty(initialText) && !initialText.Contains('\n') && !initialText.Contains('\r'))
        {
            SearchBox.Text = initialText;
        }

        SearchBox.Focus();
        SearchBox.SelectAll();
        UpdateControlsState();
    }

    /// <summary>
    /// Définit le texte d'information affiché dans le compteur de résultats.
    /// </summary>
    public void SetResult(string text)
    {
        ResultLabel.Text = text;
    }

    /// <summary>
    /// Met à jour l'état lecture seule du document actif pour adapter les boutons de remplacement.
    /// </summary>
    public void SetReadOnly(bool isReadOnly)
    {
        _isReadOnly = isReadOnly;
        UpdateControlsState();
    }

    /// <summary>
    /// Met à jour l'état mode gros fichier pour désactiver le bouton « Tout remplacer ».
    /// </summary>
    public void SetLargeFileMode(bool isLargeFileMode)
    {
        _isLargeFileMode = isLargeFileMode;
        UpdateControlsState();
    }

    private void OnSearchBoxTextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateControlsState();
        SearchChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnMatchCaseClick(object sender, RoutedEventArgs e)
    {
        SearchChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateControlsState()
    {
        bool hasSearch = !string.IsNullOrEmpty(SearchBox.Text);

        PreviousButton.IsEnabled = hasSearch;
        NextButton.IsEnabled = hasSearch;
        ReplaceButton.IsEnabled = hasSearch && !_isReadOnly;
        ReplaceAllButton.IsEnabled = hasSearch && !_isReadOnly && !_isLargeFileMode;
    }

    private void OnSearchBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            bool forward = (Keyboard.Modifiers & ModifierKeys.Shift) == 0;
            FindRequested?.Invoke(forward);
        }
    }

    private void OnReplaceBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            ReplaceRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnBarPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
