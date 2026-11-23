using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace IdeCSharp.UI;

/// <summary>Petite pastille colorée (initiales du type) placée devant le nom d'un fichier.</summary>
public static class FileTypeBadge
{
    // Extension -> (initiales affichées, couleur de fond)
    private static readonly Dictionary<string, (string Label, string Color)> Types =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".cs"] = ("C#", "#7C4DDB"),
            [".xaml"] = ("XA", "#3B82F6"),
            [".php"] = ("PHP", "#777BB3"),
            [".js"] = ("JS", "#CA8A04"),
            [".ts"] = ("TS", "#3178C6"),
            [".json"] = ("{}", "#B7791F"),
            [".html"] = ("<>", "#E44D26"),
            [".htm"] = ("<>", "#E44D26"),
            [".css"] = ("#", "#2965F1"),
            [".py"] = ("PY", "#3776AB"),
            [".java"] = ("JV", "#E76F00"),
            [".c"] = ("C", "#5C6BC0"),
            [".cpp"] = ("C+", "#00599C"),
            [".h"] = ("H", "#5C6BC0"),
            [".sql"] = ("SQL", "#E38C00"),
            [".md"] = ("MD", "#4A9BE0"),
            [".xml"] = ("XM", "#E37933"),
            [".sh"] = ("SH", "#4EAA25"),
            [".ps1"] = ("PS", "#2C5F9E"),
            [".rs"] = ("RS", "#B5651D"),
            [".go"] = ("GO", "#00ADD8"),
            [".rb"] = ("RB", "#CC342D"),
            [".txt"] = ("TX", "#6B7280"),
        };

    public static FrameworkElement Create(string path)
    {
        string ext = Path.GetExtension(path);
        var (label, hex) = Types.TryGetValue(ext, out var known) ? known : (Fallback(ext), "#6B7280");

        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze();

        return new Border
        {
            Width = 24,
            Height = 15,
            CornerRadius = new CornerRadius(3),
            Background = brush,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = label,
                FontSize = 8.5,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
    }

    // Extension inconnue : on affiche ses deux premières lettres
    private static string Fallback(string ext)
    {
        string clean = ext.TrimStart('.').ToUpperInvariant();
        return clean.Length == 0 ? "··" : clean[..Math.Min(2, clean.Length)];
    }
}