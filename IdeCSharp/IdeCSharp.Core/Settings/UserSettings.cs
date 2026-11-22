using System.Text.Json;

namespace IdeCSharp.Core.Settings;

/// <summary>Réglages de l'utilisateur, mémorisés entre deux lancements.</summary>
public class UserSettings
{
    /// <summary>« Light », « Dark » ou « System ».</summary>
    public string Theme { get; set; } = "System";

    /// <summary>Extension de thème appliquée, s'il y en a une.</summary>
    public string? ActiveExtensionId { get; set; }

    public string UiFontFamily { get; set; } = "Segoe UI";
    public double UiFontSize { get; set; } = 13;
    public string EditorFontFamily { get; set; } = "Consolas";
    public double EditorFontSize { get; set; } = 14;

    public static string Folder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Kode");

    private static string FilePath => Path.Combine(Folder, "settings.json");

    public static UserSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                return JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(FilePath)) ?? new UserSettings();
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Fichier illisible : on repart sur les réglages par défaut
        }
        return new UserSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}