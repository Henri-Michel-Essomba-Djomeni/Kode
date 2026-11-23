namespace IdeCSharp.Core.Models;

/// <summary>Résultat d'une lecture : le texte, et l'encodage détecté (à afficher dans la barre d'état).</summary>
public record FileReadResult(string Content, string EncodingName);

/// <summary>Noms des encodages gérés. Ce sont les textes affichés dans la barre d'état.</summary>
public static class FileEncodings
{
    public const string Utf8 = "UTF-8";
    public const string Utf8Bom = "UTF-8 BOM";
    public const string Utf16Le = "UTF-16 LE";
    public const string Utf16Be = "UTF-16 BE";
    public const string Utf32Le = "UTF-32 LE";
    public const string Utf32Be = "UTF-32 BE";
    public const string Windows1252 = "Windows-1252";

    /// <summary>Tous les encodages proposés à l'utilisateur.</summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        Utf8, Utf8Bom, Utf16Le, Utf16Be, Utf32Le, Utf32Be, Windows1252
    };
}
