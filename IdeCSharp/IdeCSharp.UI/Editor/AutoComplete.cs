using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;

namespace IdeCSharp.UI.Editor;

/// <summary>
/// Gère l'autocomplétion basique (mots-clés, types, extraits et mots du document)
/// pour l'éditeur de code.
/// </summary>
public static class AutoComplete
{
    /// <summary>
    /// Attache le gestionnaire d'autocomplétion à l'éditeur spécifié.
    /// </summary>
    /// <param name="editor">Composant TextEditor d'AvalonEdit.</param>
    /// <param name="languageName">Fonction fournissant le nom du langage actif.</param>
    /// <param name="isEnabled">Condition indiquant si l'autocomplétion est activée.</param>
    public static void Attach(TextEditor editor, Func<string?> languageName, Func<bool> isEnabled)
    {
        _ = new AutoCompleteHandler(editor, languageName, isEnabled);
    }

    private sealed class AutoCompleteHandler
    {
        private readonly TextEditor _editor;
        private readonly Func<string?> _languageName;
        private readonly Func<bool> _isEnabled;
        private CompletionWindow? _completionWindow;

        // Expression régulière pour extraire les mots du document (au moins 3 caractères)
        private static readonly Regex DocumentWordsRegex = new(@"\b[a-zA-Z_][a-zA-Z0-9_]{2,}\b", RegexOptions.Compiled);

        // ------------------------------------------------------------
        // Listes internes de mots-clés et types
        // ------------------------------------------------------------

        private static readonly string[] CSharpTypes =
        [
            "string", "int", "bool", "double", "decimal", "long", "char", "object", "var",
            "List", "Dictionary", "HashSet", "Console", "Math", "DateTime", "TimeSpan", "Task",
            "StringBuilder", "Exception", "Path", "File", "Directory", "Guid", "Random", "Enumerable"
        ];

        private static readonly string[] CSharpKeywords =
        [
            "abstract", "as", "base", "break", "byte", "case", "catch", "checked",
            "class", "const", "continue", "default", "delegate", "do", "else",
            "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
            "foreach", "goto", "if", "implicit", "in", "interface", "internal", "is", "lock",
            "namespace", "new", "null", "operator", "out", "override", "params",
            "private", "protected", "public", "readonly", "record", "ref", "return", "sbyte", "sealed",
            "short", "sizeof", "stackalloc", "static", "struct", "switch", "this", "throw",
            "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using",
            "virtual", "void", "volatile", "while", "yield", "async", "await", "dynamic",
            "get", "set", "init", "when", "where", "select", "group", "by", "into", "orderby",
            "join", "on", "equals", "from", "ascending", "descending"
        ];

        private static readonly string[] PythonKeywords =
        [
            "and", "as", "assert", "async", "await", "break", "class", "continue", "def", "del",
            "elif", "else", "except", "False", "finally", "for", "from", "global", "if", "import",
            "in", "is", "lambda", "None", "nonlocal", "not", "or", "pass", "raise", "return",
            "True", "try", "while", "with", "yield"
        ];

        private static readonly string[] PythonFunctions =
        [
            "print", "len", "range", "input", "int", "str", "float", "list", "dict", "set",
            "tuple", "open", "enumerate", "zip", "sorted", "sum", "min", "max", "abs"
        ];

        private static readonly HashSet<string> PythonTypesSet = new(StringComparer.Ordinal)
        {
            "int", "str", "float", "list", "dict", "set", "tuple"
        };

        public AutoCompleteHandler(TextEditor editor, Func<string?> languageName, Func<bool> isEnabled)
        {
            _editor = editor;
            _languageName = languageName;
            _isEnabled = isEnabled;

            _editor.TextArea.TextEntered += OnTextEntered;
            _editor.TextArea.PreviewKeyDown += OnPreviewKeyDown;
            _editor.Unloaded += OnEditorUnloaded;
        }

        private void OnEditorUnloaded(object sender, RoutedEventArgs e)
        {
            if (_completionWindow != null)
            {
                _completionWindow.Close();
                _completionWindow = null;
            }
        }

        private void OnTextEntered(object sender, TextCompositionEventArgs e)
        {
            if (!_isEnabled() || _editor.IsReadOnly || _completionWindow != null)
                return;

            if (string.IsNullOrEmpty(e.Text))
                return;

            char typed = e.Text[0];
            if (!char.IsLetterOrDigit(typed) && typed != '_')
                return;

            ShowCompletion(manual: false);
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Déclenchement manuel : Ctrl+Espace
            if (e.Key == Key.Space && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (!_isEnabled() || _editor.IsReadOnly)
                    return;

                e.Handled = true;
                ShowCompletion(manual: true);
            }
        }

        private void ShowCompletion(bool manual)
        {
            if (_completionWindow != null)
                return;

            var doc = _editor.Document;
            if (doc == null)
                return;

            int caret = _editor.CaretOffset;
            if (caret < 0 || caret > doc.TextLength)
                return;

            // Détection du mot en cours de saisie avant le curseur
            int wordStart = caret;
            while (wordStart > 0)
            {
                char ch = doc.GetCharAt(wordStart - 1);
                if (char.IsLetterOrDigit(ch) || ch == '_')
                {
                    wordStart--;
                }
                else
                {
                    break;
                }
            }

            int wordLength = caret - wordStart;
            string currentWord = wordLength > 0 ? doc.GetText(wordStart, wordLength) : string.Empty;

            // Caractère précédant immédiatement le début du mot
            char beforeWord = wordStart > 0 ? doc.GetCharAt(wordStart - 1) : '\0';

            // Règle : rien n'est ouvert quand le caractère juste avant le mot est un point
            if (beforeWord == '.')
                return;

            if (!manual)
            {
                // En automatique : au moins 2 caractères et ne commence pas par un chiffre
                if (currentWord.Length < 2 || char.IsDigit(currentWord[0]))
                    return;
            }
            else
            {
                // En manuel : ne commence pas par un chiffre si le mot n'est pas vide
                if (currentWord.Length > 0 && char.IsDigit(currentWord[0]))
                    return;
            }

            // Vérification de commentaire ou de chaîne de caractères
            string? language = _languageName();
            if (IsInCommentOrString(doc, caret, language))
                return;

            // Collecte des candidats
            var candidates = GetCandidates(doc, language, currentWord);

            // Filtrage par préfixe sans tenir compte de la casse
            var matching = new List<CompletionItem>();
            foreach (var item in candidates)
            {
                if (string.IsNullOrEmpty(currentWord))
                {
                    matching.Add(item);
                }
                else
                {
                    bool match = (item.Trigger != null && item.Trigger.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase))
                                 || item.Text.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase);

                    if (match)
                    {
                        matching.Add(item);
                    }
                }
            }

            // Si rien ne correspond, ne pas ouvrir de fenêtre
            if (matching.Count == 0)
                return;

            // Création et configuration de la fenêtre d'autocomplétion
            var window = new CompletionWindow(_editor.TextArea)
            {
                StartOffset = wordStart
            };

            _completionWindow = window;
            window.Closed += (_, _) => _completionWindow = null;

            // Application des styles selon les ressources de l'application
            window.SetResourceReference(Control.BackgroundProperty, "PanelBackgroundBrush");
            window.SetResourceReference(Control.ForegroundProperty, "PanelForegroundBrush");
            window.SetResourceReference(Control.BorderBrushProperty, "SplitterBrush");
            window.BorderThickness = new Thickness(1);

            window.FontFamily = _editor.FontFamily;
            window.FontSize = _editor.FontSize;

            var listBox = window.CompletionList.ListBox;
            if (listBox != null)
            {
                listBox.SetResourceReference(Control.BackgroundProperty, "PanelBackgroundBrush");
                listBox.SetResourceReference(Control.ForegroundProperty, "PanelForegroundBrush");
                listBox.FontFamily = _editor.FontFamily;
                listBox.FontSize = _editor.FontSize;
            }

            // Ajout des éléments filtrés à la liste
            var data = window.CompletionList.CompletionData;
            foreach (var item in matching)
            {
                data.Add(item);
            }

            // Sélection par défaut du premier élément
            if (data.Count > 0)
            {
                window.CompletionList.SelectedItem = data[0];
            }

            window.Show();
        }

        private static bool IsInCommentOrString(TextDocument doc, int caretOffset, string? language)
        {
            if (string.IsNullOrEmpty(language) || caretOffset <= 0)
                return false;

            var line = doc.GetLineByOffset(caretOffset);
            int lineStart = line.Offset;
            int lengthBeforeCaret = caretOffset - lineStart;
            if (lengthBeforeCaret <= 0)
                return false;

            string textBeforeCaret = doc.GetText(lineStart, lengthBeforeCaret);

            if (string.Equals(language, "C#", StringComparison.OrdinalIgnoreCase))
            {
                // Présence de commentaire sur une ligne
                if (textBeforeCaret.Contains("//"))
                    return true;

                // Nombre impair de guillemets doubles (chaîne non fermée)
                int quoteCount = 0;
                foreach (char ch in textBeforeCaret)
                {
                    if (ch == '"')
                        quoteCount++;
                }
                if (quoteCount % 2 != 0)
                    return true;
            }
            else if (string.Equals(language, "Python", StringComparison.OrdinalIgnoreCase))
            {
                // Présence de commentaire Python
                if (textBeforeCaret.Contains('#'))
                    return true;

                // Nombre impair de guillemets doubles ou simples
                int doubleQuotes = 0;
                int singleQuotes = 0;
                foreach (char ch in textBeforeCaret)
                {
                    if (ch == '"')
                        doubleQuotes++;
                    else if (ch == '\'')
                        singleQuotes++;
                }
                if (doubleQuotes % 2 != 0 || singleQuotes % 2 != 0)
                    return true;
            }

            return false;
        }

        private static List<CompletionItem> GetCandidates(TextDocument doc, string? language, string currentWord)
        {
            var list = new List<CompletionItem>();
            var seenWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (string.Equals(language, "C#", StringComparison.OrdinalIgnoreCase))
            {
                // Extraits C#
                list.Add(new CompletionItem("Console.WriteLine();", "cw  ·  extrait", "Console.WriteLine();", priority: 100, caretBack: 2, trigger: "cw"));
                list.Add(new CompletionItem("public int MyProperty { get; set; }", "prop  ·  extrait", "public int MyProperty { get; set; }", priority: 100, caretBack: 0, trigger: "prop"));
                list.Add(new CompletionItem("static void Main(string[] args)", "main  ·  extrait", "static void Main(string[] args)", priority: 100, caretBack: 0, trigger: "main"));

                seenWords.Add("cw");
                seenWords.Add("prop");
                seenWords.Add("main");

                // Types courants C#
                foreach (var type in CSharpTypes)
                {
                    list.Add(new CompletionItem(type, $"{type}  ·  type", type, priority: 50));
                    seenWords.Add(type);
                }

                // Mots-clés C#
                foreach (var kw in CSharpKeywords)
                {
                    if (seenWords.Add(kw))
                    {
                        list.Add(new CompletionItem(kw, $"{kw}  ·  mot-clé", kw, priority: 20));
                    }
                }
            }
            else if (string.Equals(language, "Python", StringComparison.OrdinalIgnoreCase))
            {
                // Fonctions et types courants Python
                foreach (var fn in PythonFunctions)
                {
                    string kind = PythonTypesSet.Contains(fn) ? "type" : "mot-clé";
                    list.Add(new CompletionItem(fn, $"{fn}  ·  {kind}", fn, priority: 50));
                    seenWords.Add(fn);
                }

                // Mots-clés Python
                foreach (var kw in PythonKeywords)
                {
                    if (seenWords.Add(kw))
                    {
                        list.Add(new CompletionItem(kw, $"{kw}  ·  mot-clé", kw, priority: 20));
                    }
                }
            }

            // Mots du document (seulement si la taille <= 1 000 000 caractères)
            if (doc.TextLength <= 1_000_000)
            {
                string text = doc.Text;
                var matches = DocumentWordsRegex.Matches(text);
                int docWordsAdded = 0;

                foreach (Match match in matches)
                {
                    string word = match.Value;

                    // Exclut le mot en cours de frappe et les mots déjà vus
                    if (string.Equals(word, currentWord, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (seenWords.Add(word))
                    {
                        list.Add(new CompletionItem(word, $"{word}  ·  document", word, priority: 1));
                        docWordsAdded++;
                        if (docWordsAdded >= 300)
                            break;
                    }
                }
            }

            return list;
        }
    }
}
