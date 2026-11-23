using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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

    private static readonly Regex ErrorLineRegex = new(
        @"^(?<file>[^\s(][^(])\((?<line>\d+),(?<col>\d+)\):\s(error|warning)\b",
        RegexOptions.Compiled);
    private string? _projectDir;

    /// <summary>Double-clic sur une erreur : chemin complet, ligne, colonne.</summary>
    public event Action<string, int, int>? ErrorActivated;

    public OutputPanel()
    {
        InitializeComponent();
        OutputBox.MouseDoubleClick += OutputBox_MouseDoubleClick;
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

        if (text.StartsWith("Compilation de ") && text.EndsWith("..."))
        {
            string p = text.Substring(15, text.Length - 18).Trim('"');
            _projectDir = File.Exists(p) ? Path.GetDirectoryName(p) : p;
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

    private void OutputBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        string all = OutputBox.Text;
        if (all.Length == 0) return;

        int index = OutputBox.GetCharacterIndexFromPoint(e.GetPosition(OutputBox), true);
        if (index < 0) return;
        if (index >= all.Length) index = all.Length - 1;

        int start = index == 0 ? 0 : all.LastIndexOf('\n', index - 1) + 1;
        int end = all.IndexOf('\n', index);
        if (end < 0) end = all.Length;

        var m = ErrorLineRegex.Match(all[start..end].Trim());
        if (!m.Success) return;

        string? path = ResolveFile(m.Groups["file"].Value);
        if (path == null) return;

        ErrorActivated?.Invoke(path, int.Parse(m.Groups["line"].Value), int.Parse(m.Groups["col"].Value));
        e.Handled = true;
    }

    private string? ResolveFile(string name)
    {
        if (Path.IsPathRooted(name) && File.Exists(name)) return name;
        if (_projectDir == null || !Directory.Exists(_projectDir)) return null;

        return Directory.EnumerateFiles(_projectDir, Path.GetFileName(name), SearchOption.AllDirectories)
            .FirstOrDefault(f => !f.Contains(@"\obj\") && !f.Contains(@"\bin\"));
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