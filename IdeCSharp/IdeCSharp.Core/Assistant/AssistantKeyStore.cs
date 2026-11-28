using System.Security.Cryptography;
using System.Text;

namespace IdeCSharp.Core.Assistant;

/// <summary>Garde la clé d'accès de l'IA, chiffrée pour l'utilisateur Windows courant (DPAPI).</summary>
public static class AssistantKeyStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Kode", "assistant.key");

    /// <summary>Renvoie la clé enregistrée, ou null s'il n'y en a pas (ou si elle est illisible).</summary>
    public static string? Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            byte[] clear = ProtectedData.Unprotect(File.ReadAllBytes(FilePath), null, DataProtectionScope.CurrentUser);
            string key = Encoding.UTF8.GetString(clear).Trim();
            return key.Length > 0 ? key : null;
        }
        catch (Exception ex) when (ex is IOException or CryptographicException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static void Save(string key)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        byte[] protectedBytes = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(key.Trim()), null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(FilePath, protectedBytes);
    }

    public static void Clear()
    {
        try { File.Delete(FilePath); }
        catch (IOException) { /* déjà absent ou verrouillé : on ignore */ }
    }
}