using System.Windows;

namespace IdeCSharp.UI.Editor;

/// <summary>
/// Boîte de dialogue permettant de saisir un numéro de ligne pour la navigation.
/// </summary>
public partial class GoToLineWindow : Window
{
    private readonly int _lineCount;

    /// <summary>
    /// Numéro de ligne choisi par l'utilisateur après validation, ou null si annulé.
    /// </summary>
    public int? Line { get; private set; }

    /// <summary>
    /// Initialise une nouvelle instance de la boîte de dialogue Aller à la ligne.
    /// </summary>
    /// <param name="currentLine">Numéro de ligne actuel du curseur.</param>
    /// <param name="lineCount">Nombre total de lignes dans le document.</param>
    public GoToLineWindow(int currentLine, int lineCount)
    {
        InitializeComponent();
        _lineCount = lineCount;

        PromptLabel.Text = $"Numéro de ligne (1 à {lineCount}) :";
        LineTextBox.Text = currentLine.ToString();

        Loaded += (_, _) =>
        {
            LineTextBox.Focus();
            LineTextBox.SelectAll();
        };
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(LineTextBox.Text.Trim(), out int value) && value >= 1 && value <= _lineCount)
        {
            Line = value;
            DialogResult = true;
        }
        else
        {
            ErrorText.Text = $"Entrez un nombre entre 1 et {_lineCount}.";
            ErrorText.Visibility = Visibility.Visible;
            LineTextBox.Focus();
            LineTextBox.SelectAll();
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
