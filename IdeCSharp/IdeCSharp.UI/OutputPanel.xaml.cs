using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using IdeCSharp.Core.Interfaces;

namespace IdeCSharp.UI;

/// <summary>
/// Panneau Sortie (tâche T46) : affiche la sortie du programme lancé,
/// avec boutons Effacer / Copier et un champ de saisie relié à l'entrée du programme.
/// </summary>
public partial class OutputPanel : UserControl
{
    private const int MaxChars = 500_000;   // au-delà, on supprime le début pour garder une interface fluide

    private IExecutionService? _execution;
    private int _chars;

    public OutputPanel()
    {
        InitializeComponent();
    }

    /// <summary>Branche le panneau sur le service d'exécution : tout ce qu'il émet s'affiche ici.</summary>
    public void Bind(IExecutionService execution)
    {
        if (_execution != null)
            _execution.OutputReceived -= AppendLine;

        _execution = execution;
        _execution.OutputReceived += AppendLine;
    }

    /// <summary>Ajoute une ligne à la sortie. Peut être appelée depuis n'importe quel thread.</summary>
    public void AppendLine(string text)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.InvokeAsync(() => AppendLine(text));
            return;
        }

        OutputBox.AppendText(text + Environment.NewLine);
        _chars += text.Length + Environment.NewLine.Length;

        if (_chars > MaxChars)
        {
            string tout = OutputBox.Text;
            OutputBox.Text = tout.Substring(tout.Length - MaxChars / 2);
            _chars = OutputBox.Text.Length;
        }

        OutputBox.ScrollToEnd();
    }

    /// <summary>Efface toute la sortie.</summary>
    public void Clear()
    {
        OutputBox.Clear();
        _chars = 0;
    }

    private void Clear_Click(object sender, RoutedEventArgs e) => Clear();

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (OutputBox.Text.Length == 0) return;
        try
        {
            Clipboard.SetText(OutputBox.Text);
        }
        catch (COMException)
        {
            // Le presse-papiers est parfois occupé par une autre application : on ignore
        }
    }

    private void Send_Click(object sender, RoutedEventArgs e) => Send();

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Send();
            e.Handled = true;
        }
    }

    private void Send()
    {
        string text = InputBox.Text;
        InputBox.Clear();

        AppendLine(text);              // le programme ne renvoie pas la saisie : on l'affiche nous-mêmes
        _execution?.SendInput(text);
    }
}