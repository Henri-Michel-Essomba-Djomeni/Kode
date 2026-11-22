// Seuils provisoires, à confirmer par l'étude T33 et les tests T36.

using System.Globalization;

namespace IdeCSharp.UI.Editor;

/// <summary>
/// Politique de gestion des gros fichiers dans l'éditeur.
/// Centralise les seuils et les fonctions utilitaires associées.
/// </summary>
public static class LargeFilePolicy
{
    /// <summary>
    /// Au-delà de ce nombre de caractères, le fichier est considéré comme « gros » :
    /// la coloration syntaxique et la saisie automatique sont désactivées.
    /// </summary>
    public const int LargeFileChars = 10_000_000;

    /// <summary>
    /// Au-delà de ce nombre de caractères, le fichier est en outre ouvert en lecture seule.
    /// </summary>
    public const int ReadOnlyChars = 50_000_000;

    /// <summary>
    /// Indique si la longueur du texte justifie le mode gros fichier.
    /// </summary>
    public static bool IsLarge(int length) => length >= LargeFileChars;

    /// <summary>
    /// Indique si la longueur du texte justifie l'ouverture en lecture seule.
    /// </summary>
    public static bool IsReadOnly(int length) => length >= ReadOnlyChars;

    /// <summary>
    /// Formate la taille en mégaoctets (1 caractère ≈ 1 octet) avec la culture française.
    /// Exemple : 12 345 678 → "11,8 Mo".
    /// </summary>
    public static string FormatSize(int chars)
    {
        double mo = chars / (1024.0 * 1024.0);
        return mo.ToString("F1", CultureInfo.GetCultureInfo("fr-FR")) + " Mo";
    }
}
