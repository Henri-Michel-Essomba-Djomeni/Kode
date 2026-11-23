using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Highlighting;

namespace IdeCSharp.UI.Editor;

/// <summary>
/// Liste des langages colorés par Kode et palette de couleurs selon le thème clair ou sombre.
/// Les noms sont ceux des définitions AvalonEdit ; « C++ » couvre aussi les fichiers .c.
/// Unique fichier du projet où les couleurs de coloration sont écrites en dur.
/// </summary>
public static class SyntaxTheme
{
    // Les 14 langages exécutables de Kode, plus XML, CSS et JSON
    private static readonly string[] SupportedNames =
    {
        "C#", "C++", "Java", "JavaScript", "PHP", "PowerShell", "Python",
        "Rust", "Go", "Ruby", "Lua", "Batch", "HTML", "XML", "CSS", "JSON"
    };

    /// <summary>Vrai si Kode sait colorer la définition de ce nom.</summary>
    public static bool IsSupported(string? name) =>
        name != null && SupportedNames.Contains(name, StringComparer.OrdinalIgnoreCase);

    public static bool IsDarkTheme()
    {
        if (Application.Current?.TryFindResource("PanelBackgroundBrush") is SolidColorBrush brush)
        {
            double luminance = (0.299 * brush.Color.R + 0.587 * brush.Color.G + 0.114 * brush.Color.B) / 255.0;
            return luminance < 0.5;
        }

        return false;
    }

    public static void Apply(bool dark)
    {
        // Les définitions personnalisées doivent exister avant d'être recolorées
        ExtraLanguages.Register();

        foreach (var language in SupportedNames)
        {
            var definition = HighlightingManager.Instance.GetDefinition(language);
            if (definition == null)
                continue;

            foreach (var color in definition.NamedHighlightingColors)
            {
                color.Foreground = GetBrush(color.Name ?? string.Empty, dark);
            }
        }
    }

    // Écrit une couleur sous la forme 0xRRGGBB : une pour le thème sombre, une pour le clair
    private static SimpleHighlightingBrush Brush(bool dark, int darkRgb, int lightRgb)
    {
        int v = dark ? darkRgb : lightRgb;
        return new SimpleHighlightingBrush(Color.FromRgb((byte)(v >> 16), (byte)(v >> 8), (byte)v));
    }

    private static bool Has(string text, params string[] parts) => parts.Any(text.Contains);

    // On devine le rôle d'une couleur d'après son nom. Retourner null laisse la couleur
    // normale de l'éditeur (ponctuation, opérateurs...).
    private static SimpleHighlightingBrush? GetBrush(string name, bool dark)
    {
        string l = name.ToLowerInvariant();

        if (Has(l, "comment", "xmldoc", "doctags"))
            return Brush(dark, 0x6A9955, 0x008000);
        if (Has(l, "doctype", "xmldeclaration", "cdata", "preprocessor"))
            return Brush(dark, 0x9B9B9B, 0x808080);
        if (l.Contains("regex"))
            return Brush(dark, 0xD16969, 0x811F3F);
        if (Has(l, "attributevalue", "string", "char"))
            return Brush(dark, 0xCE9178, 0xA31515);
        if (Has(l, "attribute", "property", "variable"))
            return Brush(dark, 0x9CDCFE, 0x0451A5);
        if (Has(l, "tag", "selector"))
            return Brush(dark, 0x569CD6, 0x800000);
        if (Has(l, "entit", "number", "digit") || l == "value")
            return Brush(dark, 0xB5CEA8, 0x098658);
        if (l == "class")
            return Brush(dark, 0x4EC9B0, 0x267F99);
        if (Has(l, "keyword", "modifier", "visibility", "true", "null", "this", "type", "semantic",
                   "getset", "context", "statement", "exceptionhandling", "controlflow", "void",
                   "package", "namespace", "friend", "literal", "constant", "intrinsic"))
            return Brush(dark, 0x569CD6, 0x0000FF);
        if (Has(l, "method", "function", "command"))
            return Brush(dark, 0xDCDCAA, 0x795E26);

        return null;
    }
}