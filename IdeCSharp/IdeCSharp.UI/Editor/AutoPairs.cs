using System;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;

namespace IdeCSharp.UI.Editor;

/// <summary>
/// Gère la fermeture automatique des paires (parenthèses, crochets, accolades, guillemets)
/// et l'indentation lors de l'appui sur Entrée entre accolades.
/// </summary>
public static class AutoPairs
{
    /// <summary>
    /// Attache la logique de fermeture automatique des paires au TextArea de l'éditeur spécifié.
    /// </summary>
    /// <param name="editor">Composant TextEditor d'AvalonEdit.</param>
    /// <param name="isEnabled">Condition déterminant si l'auto-fermeture est active.</param>
    public static void Attach(TextEditor editor, Func<bool> isEnabled)
    {
        _ = new AutoPairsHandler(editor, isEnabled);
    }

    private sealed class AutoPairsHandler
    {
        private readonly TextEditor _editor;
        private readonly Func<bool> _isEnabled;
        private char _pendingClosingChar;
        private bool _shouldInsertClosing;

        public AutoPairsHandler(TextEditor editor, Func<bool> isEnabled)
        {
            _editor = editor;
            _isEnabled = isEnabled;

            _editor.TextArea.TextEntering += OnTextEntering;
            _editor.TextArea.TextEntered += OnTextEntered;
            _editor.TextArea.PreviewKeyDown += OnPreviewKeyDown;
        }

        private void OnTextEntering(object sender, TextCompositionEventArgs e)
        {
            _shouldInsertClosing = false;
            _pendingClosingChar = '\0';

            if (!_isEnabled() || _editor.IsReadOnly || string.IsNullOrEmpty(e.Text) || e.Text.Length != 1)
                return;

            char typed = e.Text[0];
            int caret = _editor.CaretOffset;

            // Règle : si l'utilisateur tape un caractère fermant déjà présent immédiatement après le curseur,
            // on n'insère rien et on avance simplement le curseur d'un cran.
            if ((typed == ')' || typed == ']' || typed == '}' || typed == '"' || typed == '\'') && _editor.SelectionLength == 0)
            {
                if (caret < _editor.Document.TextLength && _editor.Document.GetCharAt(caret) == typed)
                {
                    e.Handled = true;
                    _editor.CaretOffset = caret + 1;
                    return;
                }
            }

            // Règle : parenthèses, crochets et accolades ouvrantes sans texte sélectionné
            if ((typed == '(' || typed == '[' || typed == '{') && _editor.SelectionLength == 0)
            {
                _shouldInsertClosing = true;
                _pendingClosingChar = typed switch
                {
                    '(' => ')',
                    '[' => ']',
                    '{' => '}',
                    _ => '\0'
                };
                return;
            }

            // Règle : guillemet ou apostrophe sans texte sélectionné
            // N'ajoute le fermant que si ni le caractère précédent ni le suivant ne sont lettre ou chiffre
            if ((typed == '"' || typed == '\'') && _editor.SelectionLength == 0)
            {
                char prevChar = caret > 0 ? _editor.Document.GetCharAt(caret - 1) : '\0';
                char nextChar = caret < _editor.Document.TextLength ? _editor.Document.GetCharAt(caret) : '\0';

                if (!char.IsLetterOrDigit(prevChar) && !char.IsLetterOrDigit(nextChar))
                {
                    _shouldInsertClosing = true;
                    _pendingClosingChar = typed;
                }
            }
        }

        private void OnTextEntered(object sender, TextCompositionEventArgs e)
        {
            if (_shouldInsertClosing && _pendingClosingChar != '\0')
            {
                int caret = _editor.CaretOffset;
                _editor.Document.Insert(caret, _pendingClosingChar.ToString());
                _editor.CaretOffset = caret;

                _shouldInsertClosing = false;
                _pendingClosingChar = '\0';
            }
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
            {
                if (!_isEnabled() || _editor.IsReadOnly || _editor.SelectionLength > 0)
                    return;

                int caret = _editor.CaretOffset;
                if (caret > 0 && caret < _editor.Document.TextLength)
                {
                    char before = _editor.Document.GetCharAt(caret - 1);
                    char after = _editor.Document.GetCharAt(caret);

                    // Quand le curseur est exactement entre "{" et "}"
                    if (before == '{' && after == '}')
                    {
                        e.Handled = true;

                        var line = _editor.Document.GetLineByOffset(caret);
                        string lineText = _editor.Document.GetText(line.Offset, line.Length);
                        string baseIndentation = GetLeadingWhitespace(lineText);
                        string indentString = _editor.Options.IndentationString ?? "    ";
                        string innerIndentation = baseIndentation + indentString;

                        string newLine = Environment.NewLine;
                        string toInsert = newLine + innerIndentation + newLine + baseIndentation;

                        using (_editor.Document.RunUpdate())
                        {
                            _editor.Document.Insert(caret, toInsert);
                        }

                        // Curseur placé sur la ligne du milieu
                        _editor.CaretOffset = caret + newLine.Length + innerIndentation.Length;
                    }
                }
            }
        }

        private static string GetLeadingWhitespace(string text)
        {
            int i = 0;
            while (i < text.Length && (text[i] == ' ' || text[i] == '\t'))
            {
                i++;
            }
            return text.Substring(0, i);
        }
    }
}
