using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Rendering;

namespace IdeCSharp.UI.Editor;

/// <summary>
/// Vue d'un document unique : encapsule un TextEditor AvalonEdit
/// avec le suivi de modification, l'annulation et la navigation.
/// </summary>
public partial class DocumentView : UserControl
{
    // Drapeau empêchant de passer en « modifié » pendant un LoadText.
    private bool _chargementEnCours;
    private bool _isModified;

    /// <summary>
    /// Initialise une nouvelle instance du composant <see cref="DocumentView"/>.
    /// </summary>
    public DocumentView()
    {
        InitializeComponent();

        // Surlignage de la ligne courante sans cadre, synchronisé avec le thème
        CodeEditor.Options.HighlightCurrentLine = true;
        CodeEditor.TextArea.TextView.SetResourceReference(TextView.CurrentLineBackgroundProperty, "SplitterBrush");
        CodeEditor.TextArea.TextView.CurrentLineBorder = new Pen(Brushes.Transparent, 0);

        // Raccourci Ctrl+Maj+Z pour Rétablir (local au TextArea uniquement)
        CodeEditor.TextArea.InputBindings.Add(
            new KeyBinding(ApplicationCommands.Redo, Key.Z, ModifierKeys.Control | ModifierKeys.Shift));

        // Suivi de la modification du texte par l'utilisateur
        CodeEditor.TextChanged += OnTextChanged;

        // Suivi de la position du curseur
        CodeEditor.TextArea.Caret.PositionChanged += (_, _) =>
        {
            var caret = CodeEditor.TextArea.Caret;
            CaretPositionChanged?.Invoke(caret.Line, caret.Column);
        };

        // Gestion du thème pour la coloration syntaxique
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SyntaxTheme.Apply(SyntaxTheme.IsDarkTheme());
        ThemeManager.ThemeChanged += OnThemeChanged;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ThemeManager.ThemeChanged -= OnThemeChanged;
    }

    private void OnThemeChanged(bool dark)
    {
        SyntaxTheme.Apply(dark);
        CodeEditor.TextArea.TextView.Redraw();
    }

    // ----------------------------------------------------------------
    // Langage et coloration syntaxique
    // ----------------------------------------------------------------

    /// <summary>
    /// Nom de la définition de coloration syntaxique active, ou « Texte brut ».
    /// </summary>
    public string LanguageName => CodeEditor.SyntaxHighlighting?.Name ?? "Texte brut";

    /// <summary>
    /// Définit la coloration syntaxique selon l'extension du fichier (null / texte brut si inconnue).
    /// </summary>
    public void SetLanguageFromPath(string path)
    {
        string extension = Path.GetExtension(path);
        CodeEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinitionByExtension(extension);
    }

    // ----------------------------------------------------------------
    // Texte et chargement
    // ----------------------------------------------------------------

    /// <summary>
    /// Obtient le texte actuellement contenu dans l'éditeur.
    /// </summary>
    public string Text => CodeEditor.Text;

    /// <summary>
    /// Charge un nouveau texte dans l'éditeur, vide l'historique
    /// d'annulation et remet l'état de modification à faux.
    /// </summary>
    public void LoadText(string text)
    {
        _chargementEnCours = true;
        try
        {
            CodeEditor.Text = text;
            CodeEditor.Document.UndoStack.ClearAll();
            SetModified(false);
        }
        finally
        {
            _chargementEnCours = false;
        }
    }

    // ----------------------------------------------------------------
    // Modification
    // ----------------------------------------------------------------

    /// <summary>
    /// Indique si le texte a été modifié depuis le dernier chargement
    /// ou le dernier appel à <see cref="MarkAsSaved"/>.
    /// </summary>
    public bool IsModified => _isModified;

    /// <summary>
    /// Déclenché quand la valeur de <see cref="IsModified"/> change.
    /// </summary>
    public event EventHandler? ModifiedChanged;

    /// <summary>
    /// Marque le document comme sauvegardé (non modifié).
    /// </summary>
    public void MarkAsSaved()
    {
        SetModified(false);
    }

    private void SetModified(bool value)
    {
        if (_isModified == value)
            return;
        _isModified = value;
        ModifiedChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnTextChanged(object? sender, EventArgs e)
    {
        // Pendant un LoadText on ne touche pas au drapeau
        if (!_chargementEnCours)
            SetModified(true);
    }

    // ----------------------------------------------------------------
    // Annuler / Rétablir
    // ----------------------------------------------------------------

    /// <summary>
    /// Annule la dernière action si possible.
    /// </summary>
    public void Undo()
    {
        if (CodeEditor.Document.UndoStack.CanUndo)
            CodeEditor.Undo();
    }

    /// <summary>
    /// Rétablit la dernière action annulée si possible.
    /// </summary>
    public void Redo()
    {
        if (CodeEditor.Document.UndoStack.CanRedo)
            CodeEditor.Redo();
    }

    // ----------------------------------------------------------------
    // Navigation
    // ----------------------------------------------------------------

    /// <summary>
    /// Place le curseur à la ligne et colonne indiquées, fait défiler
    /// la vue pour rendre le curseur visible et donne le focus.
    /// Les valeurs sont bornées aux limites réelles du document.
    /// </summary>
    public void GoTo(int line, int column)
    {
        int totalLines = CodeEditor.Document.LineCount;

        // Borner la ligne entre 1 et le nombre total de lignes
        line = Math.Clamp(line, 1, totalLines);

        var docLine = CodeEditor.Document.GetLineByNumber(line);
        int maxColumn = docLine.Length + 1;

        // Borner la colonne entre 1 et la longueur de la ligne + 1
        column = Math.Clamp(column, 1, maxColumn);

        CodeEditor.TextArea.Caret.Line = line;
        CodeEditor.TextArea.Caret.Column = column;
        CodeEditor.ScrollToLine(line);
        CodeEditor.TextArea.Focus();
    }

    // ----------------------------------------------------------------
    // Position du curseur
    // ----------------------------------------------------------------

    /// <summary>
    /// Déclenché quand la position du curseur change (ligne, colonne).
    /// </summary>
    public event Action<int, int>? CaretPositionChanged;

    // ----------------------------------------------------------------
    // Focus
    // ----------------------------------------------------------------

    /// <summary>
    /// Donne le focus au composant d'édition de code.
    /// </summary>
    public void FocusEditor()
    {
        CodeEditor.Focus();
    }
}
