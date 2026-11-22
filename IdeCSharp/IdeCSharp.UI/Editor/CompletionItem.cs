using System;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;

namespace IdeCSharp.UI.Editor;

/// <summary>
/// Élément de complétion proposé dans la fenêtre d'autocomplétion.
/// Implémente l'interface <see cref="ICompletionData"/> d'AvalonEdit.
/// </summary>
public class CompletionItem : ICompletionData
{
    public string Text { get; }
    public object Content { get; }
    public object Description { get; }
    public ImageSource? Image => null;
    public double Priority { get; }
    public int CaretBack { get; }

    /// <summary>
    /// Déclencheur ou préfixe abrégé (par exemple "cw" pour un extrait).
    /// </summary>
    public string? Trigger { get; }

    public CompletionItem(string text, string content, string description = "", double priority = 0, int caretBack = 0, string? trigger = null)
    {
        Text = text;
        Content = content;
        Description = description;
        Priority = priority;
        CaretBack = caretBack;
        Trigger = trigger;
    }

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        textArea.Document.Replace(completionSegment, Text);
        if (CaretBack > 0)
        {
            textArea.Caret.Offset = Math.Max(0, textArea.Caret.Offset - CaretBack);
        }
    }
}
