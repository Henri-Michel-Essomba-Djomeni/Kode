using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
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
    private static readonly HashSet<string> SupportedLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        "C#", "XML", "HTML", "Python", "JSON"
    };

    // Drapeau empêchant de passer en « modifié » pendant un LoadText.
    private bool _chargementEnCours;
    private bool _isModified;

    // Chemin mémorisé pour rétablir la coloration en sortant du mode gros fichier
    private string? _languagePath;

    // Indique si le mode gros fichier est actif
    private bool _isLargeFileMode;

    /// <summary>
    /// Initialise une nouvelle instance du composant <see cref="DocumentView"/>.
    /// </summary>
    public DocumentView()
    {
        InitializeComponent();

        // Enregistrement des langages supplémentaires (Python, JSON)
        ExtraLanguages.Register();

        // Stratégie d'indentation par défaut
        CodeEditor.TextArea.IndentationStrategy = new DefaultIndentationStrategy();

        // Surlignage de la ligne courante sans cadre, synchronisé avec le thème
        CodeEditor.Options.HighlightCurrentLine = true;
        CodeEditor.TextArea.TextView.SetResourceReference(TextView.CurrentLineBackgroundProperty, "SplitterBrush");
        CodeEditor.TextArea.TextView.CurrentLineBorder = new Pen(Brushes.Transparent, 0);

        // Liaison avec les paramètres pour la police et la taille de base (T24)
        if (Application.Current?.TryFindResource("EditorFontSize") is double)
        {
            SetResourceReference(BaseFontSizeProperty, "EditorFontSize");
        }
        else
        {
            BaseFontSize = 14.0;
        }

        if (Application.Current?.TryFindResource("EditorFontFamily") is FontFamily)
        {
            CodeEditor.SetResourceReference(Control.FontFamilyProperty, "EditorFontFamily");
        }
        else
        {
            CodeEditor.FontFamily = new FontFamily("Consolas");
        }

        ApplyFontSize();

        // Gestionnaires locaux pour le zoom (T24)
        CodeEditor.PreviewMouseWheel += OnCodeEditorPreviewMouseWheel;
        CodeEditor.PreviewKeyDown += OnCodeEditorPreviewKeyDown;

        // Raccourci Ctrl+Maj+Z pour Rétablir (local au TextArea uniquement)
        CodeEditor.TextArea.InputBindings.Add(
            new KeyBinding(ApplicationCommands.Redo, Key.Z, ModifierKeys.Control | ModifierKeys.Shift));

        // Fermeture automatique des paires (désactivée en mode gros fichier ou lecture seule)
        AutoPairs.Attach(CodeEditor, () => CodeEditor.SyntaxHighlighting != null && !CodeEditor.IsReadOnly && !IsLargeFileMode);

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
    /// Nom de la définition de coloration syntaxique active ("C#", "XML", "HTML", "Python", "JSON"), ou « Texte brut ».
    /// </summary>
    public string LanguageName => CodeEditor.SyntaxHighlighting?.Name ?? "Texte brut";

    /// <summary>
    /// Définit la coloration syntaxique et la stratégie d'indentation selon l'extension du fichier.
    /// Seuls les langages supportés (C#, XML, HTML, Python, JSON) sont activés, le reste étant traité en texte brut.
    /// Mémorise le chemin pour pouvoir rétablir la coloration en sortant du mode gros fichier.
    /// </summary>
    public void SetLanguageFromPath(string path)
    {
        _languagePath = path;

        // En mode gros fichier, on ne rétablit pas la coloration
        if (_isLargeFileMode)
            return;

        ApplyLanguageFromPath(path);
    }

    private void ApplyLanguageFromPath(string path)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();
        var definition = HighlightingManager.Instance.GetDefinitionByExtension(extension);

        if (definition != null && SupportedLanguages.Contains(definition.Name))
        {
            CodeEditor.SyntaxHighlighting = definition;
        }
        else
        {
            CodeEditor.SyntaxHighlighting = null;
        }

        if (CodeEditor.SyntaxHighlighting != null && string.Equals(CodeEditor.SyntaxHighlighting.Name, "C#", StringComparison.OrdinalIgnoreCase))
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
    /// Évalue ensuite le mode gros fichier d'après la longueur du texte.
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

        // Évaluation du mode gros fichier après le chargement
        EvaluateLargeFileMode(text.Length);
    }

    // ----------------------------------------------------------------
    // Mode gros fichier (T35)
    // ----------------------------------------------------------------

    /// <summary>
    /// Indique si le mode gros fichier est actif pour ce document.
    /// </summary>
    public bool IsLargeFileMode => _isLargeFileMode;

    /// <summary>
    /// Évalue si le texte chargé nécessite le mode gros fichier et l'applique.
    /// </summary>
    private void EvaluateLargeFileMode(int textLength)
    {
        bool shouldBeLarge = LargeFilePolicy.IsLarge(textLength);

        if (shouldBeLarge && !_isLargeFileMode)
        {
            ActivateLargeFileMode(textLength);
        }
        else if (!shouldBeLarge && _isLargeFileMode)
        {
            DeactivateLargeFileMode();
        }
        else if (shouldBeLarge && _isLargeFileMode)
        {
            // Mise à jour du bandeau si la taille a changé (nouveau texte dans un document déjà en mode gros fichier)
            UpdateLargeFileBanner(textLength);
        }
    }

    private void ActivateLargeFileMode(int textLength)
    {
        _isLargeFileMode = true;

        // Désactivation de la coloration syntaxique
        CodeEditor.SyntaxHighlighting = null;

        // Retour à la ligne désactivé et hyperliens désactivés
        CodeEditor.WordWrap = false;
        CodeEditor.Options.EnableHyperlinks = false;
        CodeEditor.Options.EnableEmailHyperlinks = false;

        // Stratégie d'indentation par défaut
        CodeEditor.TextArea.IndentationStrategy = new DefaultIndentationStrategy();

        // Lecture seule si le fichier dépasse le seuil
        if (LargeFilePolicy.IsReadOnly(textLength))
        {
            CodeEditor.IsReadOnly = true;
        }

        // Affichage du bandeau d'information
        UpdateLargeFileBanner(textLength);
    }

    private void DeactivateLargeFileMode()
    {
        _isLargeFileMode = false;

        // Rétablir les options par défaut
        CodeEditor.Options.EnableHyperlinks = true;
        CodeEditor.Options.EnableEmailHyperlinks = true;
        CodeEditor.IsReadOnly = false;

        // Rétablir la coloration syntaxique à partir du chemin mémorisé
        if (_languagePath != null)
        {
            ApplyLanguageFromPath(_languagePath);
        }

        // Masquer le bandeau
        LargeFileBanner.Visibility = Visibility.Collapsed;
    }

    private void UpdateLargeFileBanner(int textLength)
    {
        string taille = LargeFilePolicy.FormatSize(textLength);
        string message = $"Mode gros fichier ({taille}) : coloration et saisie automatique désactivées.";

        if (LargeFilePolicy.IsReadOnly(textLength))
        {
            message += " Lecture seule.";
        }

        LargeFileBannerText.Text = message;
        LargeFileBanner.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Libère la mémoire du document en remplaçant son contenu par un document vide.
    /// Ne déclenche pas le marquage « modifié ».
    /// </summary>
    public void ReleaseMemory()
    {
        _chargementEnCours = true;
        try
        {
            CodeEditor.Document = new TextDocument();
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
    /// Retourne 0 et ne fait rien en mode gros fichier.
    /// </summary>
    public int ReplaceAll(string search, string replacement, bool matchCase)
    {
        // Désactivé en mode gros fichier
        if (_isLargeFileMode)
            return 0;

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
    /// Retourne Total = -1 si le document dépasse 2 000 000 caractères ou en mode gros fichier.
    /// </summary>
    public (int Index, int Total) GetMatchInfo(string search, bool matchCase)
    {
        if (string.IsNullOrEmpty(search))
            return (0, 0);

        // En mode gros fichier, le décompte est désactivé
        if (_isLargeFileMode)
            return (0, -1);

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

    // ----------------------------------------------------------------
    // Police et zoom (T24)
    // ----------------------------------------------------------------

    /// <summary>
    /// Identifie la propriété de dépendance <see cref="BaseFontSize"/>.
    /// </summary>
    public static readonly DependencyProperty BaseFontSizeProperty =
        DependencyProperty.Register(
            nameof(BaseFontSize),
            typeof(double),
            typeof(DocumentView),
            new PropertyMetadata(14.0, (d, _) => ((DocumentView)d).ApplyFontSize()));

    /// <summary>
    /// Taille de base de la police de l'éditeur (suit en direct les paramètres d'application).
    /// </summary>
    public double BaseFontSize
    {
        get => (double)GetValue(BaseFontSizeProperty);
        set => SetValue(BaseFontSizeProperty, value);
    }

    private int _zoomOffset;

    /// <summary>
    /// Décalage de zoom appliqué par-dessus la taille de base.
    /// </summary>
    public int ZoomOffset
    {
        get => _zoomOffset;
        set
        {
            _zoomOffset = value;
            ApplyFontSize();
        }
    }

    /// <summary>
    /// Taille effective actuelle de la police appliquée dans l'éditeur.
    /// </summary>
    public double EditorFontSize => CodeEditor.FontSize;

    /// <summary>
    /// Déclenché lorsqu'un incrément ou décrément de zoom est demandé (+1 ou -1).
    /// </summary>
    public event Action<int>? ZoomStepRequested;

    /// <summary>
    /// Déclenché lorsqu'une remise à zéro du zoom (décalage à zéro) est demandée.
    /// </summary>
    public event Action? ZoomResetRequested;

    private void ApplyFontSize()
    {
        if (CodeEditor != null)
        {
            CodeEditor.FontSize = Math.Clamp(BaseFontSize + ZoomOffset, 6, 48);
        }
    }

    private void OnCodeEditorPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            int sens = e.Delta > 0 ? 1 : -1;
            ZoomStepRequested?.Invoke(sens);
            e.Handled = true;
        }
    }

    private void OnCodeEditorPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Avec Ctrl enfoncé (et sans Alt pour ne pas intercepter les combinaisons AltGr)
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 && (Keyboard.Modifiers & ModifierKeys.Alt) == 0)
        {
            if (e.Key == Key.Add || e.Key == Key.OemPlus)
            {
                ZoomStepRequested?.Invoke(1);
                e.Handled = true;
            }
            else if (e.Key == Key.Subtract || e.Key == Key.OemMinus)
            {
                ZoomStepRequested?.Invoke(-1);
                e.Handled = true;
            }
            else if (e.Key == Key.D0 || e.Key == Key.NumPad0)
            {
                ZoomResetRequested?.Invoke();
                e.Handled = true;
            }
        }
    }
}
