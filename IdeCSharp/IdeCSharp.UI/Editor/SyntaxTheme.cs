using System;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Highlighting;

namespace IdeCSharp.UI.Editor;

/// <summary>
/// Gère la palette de couleurs de la coloration syntaxique selon le thème clair ou sombre
/// pour tous les langages pris en charge (C#, XML, HTML, Python, JSON, JavaScript, CSS).
/// Unique fichier du projet où les couleurs de coloration sont écrites en dur.
/// </summary>
public static class SyntaxTheme
{
    /// <summary>
    /// Détermine si le thème actuel est sombre d'après la luminance du fond du panneau.
    /// Retourne false (thème clair) par défaut si la ressource est absente.
    /// </summary>
    public static bool IsDarkTheme()
    {
        if (Application.Current?.TryFindResource("PanelBackgroundBrush") is SolidColorBrush brush)
        {
            double luminance = (0.299 * brush.Color.R + 0.587 * brush.Color.G + 0.114 * brush.Color.B) / 255.0;
            return luminance < 0.5;
        }

        return false;
    }

    /// <summary>
    /// Recolore les définitions de syntaxe prises en charge selon le thème spécifié.
    /// </summary>
    public static void Apply(bool dark)
    {
        // Enregistre les définitions personnalisées avant toute recoloration
        ExtraLanguages.Register();

        string[] languageNames = { "C#", "XML", "HTML", "Python", "JSON", "JavaScript", "CSS" };

        foreach (var lang in languageNames)
        {
            var definition = HighlightingManager.Instance.GetDefinition(lang);
            if (definition == null)
                continue;

            foreach (var color in definition.NamedHighlightingColors)
            {
                string name = color.Name ?? string.Empty;
                color.Foreground = GetBrush(name, dark);
            }
        }
    }

    private static SimpleHighlightingBrush? GetBrush(string name, bool dark)
    {
        string lower = name.ToLowerInvariant();

        // 1. Commentaire
        if (lower.Contains("comment") || lower.Contains("xmldoc"))
            return new SimpleHighlightingBrush(dark ? Color.FromRgb(0x6A, 0x99, 0x55) : Color.FromRgb(0x00, 0x80, 0x00));

        // 2. Préprocesseur
        if (lower.Contains("doctype") || lower.Contains("xmldeclaration") || lower.Contains("cdata") || lower.Contains("preprocessor"))
            return new SimpleHighlightingBrush(dark ? Color.FromRgb(0x9B, 0x9B, 0x9B) : Color.FromRgb(0x80, 0x80, 0x80));

        // 3. Chaîne
        if (lower.Contains("attributevalue") || lower.Contains("string") || lower.Contains("char"))
            return new SimpleHighlightingBrush(dark ? Color.FromRgb(0xCE, 0x91, 0x78) : Color.FromRgb(0xA3, 0x15, 0x15));

        // 4. Attribut / Propriété
        if (lower.Contains("attributename") || lower.Contains("property"))
            return new SimpleHighlightingBrush(dark ? Color.FromRgb(0x9C, 0xDC, 0xFE) : Color.FromRgb(0x04, 0x51, 0xA5));

        // 5. Balise
        if (lower.Contains("tag"))
            return new SimpleHighlightingBrush(dark ? Color.FromRgb(0x56, 0x9C, 0xD6) : Color.FromRgb(0x80, 0x00, 0x00));

        // 6. Nombre
        if (lower.Contains("entity") || lower.Contains("number") || lower.Contains("digit"))
            return new SimpleHighlightingBrush(dark ? Color.FromRgb(0xB5, 0xCE, 0xA8) : Color.FromRgb(0x09, 0x86, 0x58));

        // 7. Mot-clé
        if (lower.Contains("keyword") || lower.Contains("modifier") || lower.Contains("visibility") ||
            lower.Contains("true") || lower.Contains("null") || lower.Contains("this") ||
            lower.Contains("type") || lower.Contains("semantic") || lower.Contains("getset") ||
            lower.Contains("context"))
        {
            return new SimpleHighlightingBrush(dark ? Color.FromRgb(0x56, 0x9C, 0xD6) : Color.FromRgb(0x00, 0x00, 0xFF));
        }

        // 8. Appel de méthode
        if (lower.Contains("method"))
            return new SimpleHighlightingBrush(dark ? Color.FromRgb(0xDC, 0xDC, 0xAA) : Color.FromRgb(0x79, 0x5E, 0x26));

        // 9. Tout autre nom
        return null;
    }
}
