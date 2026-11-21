using System;
using System.Windows.Controls;

namespace IdeCSharp.UI.Editor;

/// <summary>
/// Contrôle d'édition de code source basé sur AvalonEdit.
/// </summary>
public partial class EditorView : UserControl
{
    /// <summary>
    /// Initialise une nouvelle instance du composant <see cref="EditorView"/>.
    /// </summary>
    public EditorView()
    {
        InitializeComponent();
        CodeEditor.TextChanged += (sender, args) => TextChanged?.Invoke(this, args);
    }

    /// <summary>
    /// Obtient ou définit le texte contenu dans l'éditeur de code.
    /// </summary>
    public string Text
    {
        get => CodeEditor.Text;
        set => CodeEditor.Text = value;
    }

    /// <summary>
    /// Événement déclenché lorsque le contenu texte de l'éditeur change.
    /// </summary>
    public event EventHandler? TextChanged;

    /// <summary>
    /// Donne le focus au composant d'édition de code.
    /// </summary>
    public void FocusEditor()
    {
        CodeEditor.Focus();
    }
}
