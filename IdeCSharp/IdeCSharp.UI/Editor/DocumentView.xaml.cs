using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Indentation;
using ICSharpCode.AvalonEdit.Indentation.CSharp;
using ICSharpCode.AvalonEdit.Rendering;

namespace IdeCSharp.UI.Editor;

/// <summary>
/// Vue d'un document unique : encapsule un TextEditor AvalonEdit
/// avec le suivi de modification, l'annulation, l'auto-indentation et la navigation.
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

        // Stratégie d'indentation par défaut
        CodeEditor.TextArea.IndentationStrategy = new DefaultIndentationStrategy();

        // Surlignage de la ligne courante sans cadre, synchronisé avec le thème
        CodeEditor.Options.HighlightCurrentLine = true;
        CodeEditor.TextArea.TextView.SetResourceReference(TextView.CurrentLineBackgroundProperty, "SplitterBrush");
        CodeEditor.TextArea.TextView.CurrentLineBorder = new Pen(Brushes.Transparent, 0);

        // Raccourci Ctrl+Maj+Z pour Rétablir (local au TextArea uniquement)
        CodeEditor.TextArea.InputBindings.Add(
            new KeyBinding(ApplicationCommands.Redo, Key.Z, ModifierKeys.Control | ModifierKeys.Shift));

        // T35 : désactiver aussi en mode gros fichier
        AutoPairs.Attach(CodeEditor, () => CodeEditor.SyntaxHighlighting != null && !CodeEditor.IsReadOnly);

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
    /// Définit la coloration syntaxique et la stratégie d'indentation selon l'extension du fichier.
    /// </summary>
    public void SetLanguageFromPath(string path)
    {
        string extension = Path.GetExtension(path);
        var definition = HighlightingManager.Instance.GetDefinitionByExtension(extension);
        CodeEditor.SyntaxHighlighting = definition;

        if (definition != null && string.Equals(definition.Name, "C#", StringComparison.OrdinalIgnoreCase))
        {
            CodeEditor.TextArea.IndentationStrategy = new CSharpIndentationStrategy(CodeEditor.Options);
        }
        else
        {
            CodeEditor.TextArea.IndentationStrategy = new DefaultIndentationStrategy();
        }
    }

    // ----------------------------------------------------------------
    // Texte et chargement
    // ----------------------------------------------------------------

    /// <summary>
    /// Obtient le texte actuellement contenu dans l'éditeur.
    /// </summary>
    public string Text => CodeEditor.Text;

    /// <summary>
    /// Obtient le texte actuellement sélectionné dans l'éditeur.
    /// </summary>
    public string SelectedText => CodeEditor.SelectedText;

    /// <summary>
    /// Obtient ou définit si l'éditeur est en lecture seule.
    /// </summary>
    public bool IsReadOnly
    {
        get => CodeEditor.IsReadOnly;
        set => CodeEditor.IsReadOnly = value;
    }

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
    // Recherche et remplacement
    // ----------------------------------------------------------------

    /// <summary>
    /// Recherche l'occurrence suivante (vers l'avant ou l'arrière) depuis la sélection avec bouclage.
    /// </summary>
    public bool FindNext(string search, bool matchCase, bool forward)
    {
        if (string.IsNullOrEmpty(search))
            return false;

        int start = forward
            ? CodeEditor.SelectionStart + CodeEditor.SelectionLength
            : CodeEditor.SelectionStart;

        int index = TextSearch.IndexOfMatch(CodeEditor.Text, search, start, matchCase, forward, wrap: true);
        if (index >= 0)
        {
            SelectAndScrollTo(index, search.Length);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Recherche vers l'avant à partir du début de la sélection (pour la frappe en temps réel).
    /// </summary>
    public bool FindFromSelectionStart(string search, bool matchCase)
    {
        if (string.IsNullOrEmpty(search))
            return false;

        int start = CodeEditor.SelectionStart;
        int index = TextSearch.IndexOfMatch(CodeEditor.Text, search, start, matchCase, forward: true, wrap: true);
        if (index >= 0)
        {
            SelectAndScrollTo(index, search.Length);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Remplace l'occurrence courante si la sélection correspond, puis navigue vers la suivante.
    /// </summary>
    public bool ReplaceCurrent(string search, string replacement, bool matchCase)
    {
        if (IsReadOnly || string.IsNullOrEmpty(search))
            return false;

        var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        if (CodeEditor.SelectedText.Equals(search, comparison))
        {
            int offset = CodeEditor.SelectionStart;
            CodeEditor.Document.Replace(offset, CodeEditor.SelectionLength, replacement);
            CodeEditor.Select(offset, replacement.Length);
            FindNext(search, matchCase, forward: true);
            return true;
        }

        return FindNext(search, matchCase, forward: true);
    }

    /// <summary>
    /// Remplace toutes les occurrences en une seule opération d'annulation.
    /// </summary>
    public int ReplaceAll(string search, string replacement, bool matchCase)
    {
        if (IsReadOnly || string.IsNullOrEmpty(search))
            return 0;

        string text = CodeEditor.Text;
        var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var positions = new List<int>();
        int idx = 0;

        while ((idx = text.IndexOf(search, idx, comparison)) >= 0)
        {
            positions.Add(idx);
            idx += search.Length;
        }

        if (positions.Count == 0)
            return 0;

        using (CodeEditor.Document.RunUpdate())
        {
            for (int i = positions.Count - 1; i >= 0; i--)
            {
                CodeEditor.Document.Replace(positions[i], search.Length, replacement);
            }
        }

        return positions.Count;
    }

    /// <summary>
    /// Obtient les informations de correspondance : rang sélectionné (1-basé, ou 0) et nombre total d'occurrences.
    /// Retourne Total = -1 si le document dépasse 2 000 000 caractères.
    /// </summary>
    public (int Index, int Total) GetMatchInfo(string search, bool matchCase)
    {
        if (string.IsNullOrEmpty(search))
            return (0, 0);

        string text = CodeEditor.Text;
        if (text.Length > 2_000_000)
            return (0, -1);

        int total = TextSearch.CountMatches(text, search, matchCase);
        int index = (CodeEditor.SelectionLength == search.Length)
            ? TextSearch.MatchIndexAt(text, search, CodeEditor.SelectionStart, matchCase)
            : 0;

        return (index, total);
    }

    private void SelectAndScrollTo(int offset, int length)
    {
        CodeEditor.Select(offset, length);
        var line = CodeEditor.Document.GetLineByOffset(offset);
        CodeEditor.ScrollToLine(line.LineNumber);
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
    /// Déclenché quand le texte de l'éditeur change.
    /// </summary>
    public event EventHandler? TextChanged;

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

        TextChanged?.Invoke(this, EventArgs.Empty);
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
    /// Nombre total de lignes dans le document.
    /// </summary>
    public int LineCount => CodeEditor.Document.LineCount;

    /// <summary>
    /// Numéro de ligne actuel du curseur (1-basé).
    /// </summary>
    public int CurrentLine => CodeEditor.TextArea.Caret.Line;

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
