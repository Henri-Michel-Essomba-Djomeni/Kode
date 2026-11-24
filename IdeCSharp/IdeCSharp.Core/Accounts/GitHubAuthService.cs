using System.Net.Http.Headers;
using System.Text.Json;

namespace IdeCSharp.Core.Accounts;

/// <summary>Profil public GitHub de la personne connectée.</summary>
public record GitHubAccount(string Login, string? Name, string? AvatarUrl);

/// <summary>Code à saisir sur GitHub pour autoriser Kode.</summary>
public record DeviceCodeInfo(string DeviceCode, string UserCode, string VerificationUri, int ExpiresIn, int Interval);

/// <summary>Erreur de réseau : GitHub n'a pas pu être joint (connexion instable, par exemple).</summary>
public sealed class GitHubNetworkException : InvalidOperationException
{
    public GitHubNetworkException(string message) : base(message) { }
}

/// <summary>
/// Connexion à un compte GitHub avec le « Device Flow » : aucun mot de passe et aucun secret dans Kode.
/// Le jeton obtenu ne sert qu'à lire le profil public, puis il est abandonné.
/// </summary>
public class GitHubAuthService
{
    // Identifiant PUBLIC de l'application OAuth « Kode » (jamais le client secret).
    // À remplacer par le Client ID copié depuis la page de ton application GitHub.
    public const string ClientId = "Ov23liIgyL9zzudn6xfv";

    private const string NetworkMessage = "Impossible de joindre GitHub. Vérifie ta connexion internet.";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Kode-IDE");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    // ------------------------------------------------------------------
    // Étape 1 : obtenir un code à saisir sur GitHub
    // ------------------------------------------------------------------

    public async Task<DeviceCodeInfo> RequestDeviceCodeAsync(CancellationToken cancellationToken)
    {
        using var doc = await PostFormAsync(
            "https://github.com/login/device/code",
            new Dictionary<string, string> { ["client_id"] = ClientId },
            cancellationToken);

        var root = doc.RootElement;
        if (root.TryGetProperty("error", out var error))
            throw new InvalidOperationException(Describe(error.GetString()));

        return new DeviceCodeInfo(
            root.GetProperty("device_code").GetString()!,
            root.GetProperty("user_code").GetString()!,
            root.GetProperty("verification_uri").GetString()!,
            root.GetProperty("expires_in").GetInt32(),
            root.GetProperty("interval").GetInt32());
    }

    // ------------------------------------------------------------------
    // Étape 2 : attendre que la personne valide, puis lire son profil
    // ------------------------------------------------------------------

    public async Task<GitHubAccount> WaitForAuthorizationAsync(DeviceCodeInfo info, CancellationToken cancellationToken)
    {
        int interval = Math.Max(info.Interval, 5);
        var deadline = DateTime.UtcNow.AddSeconds(info.ExpiresIn);

        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromSeconds(interval), cancellationToken);

            try
            {
                using var doc = await PostFormAsync(
                    "https://github.com/login/oauth/access_token",
                    new Dictionary<string, string>
                    {
                        ["client_id"] = ClientId,
                        ["device_code"] = info.DeviceCode,
                        ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code"
                    },
                    cancellationToken);

                var root = doc.RootElement;

                if (root.TryGetProperty("access_token", out var token))
                    return await FetchProfileAsync(token.GetString()!, cancellationToken);

                string error = root.TryGetProperty("error", out var e) ? e.GetString() ?? string.Empty : string.Empty;
                switch (error)
                {
                    case "authorization_pending":
                        break;                       // pas encore validé : on réessaie
                    case "slow_down":
                        interval += 5;               // GitHub demande d'espacer les essais
                        break;
                    default:
                        throw new InvalidOperationException(Describe(error));
                }
            }
            catch (GitHubNetworkException)
            {
                // Coupure passagère (internet instable) : on réessaie au prochain tour
            }
        }

        throw new InvalidOperationException("Le code a expiré. Recommence la connexion.");
    }

    private static async Task<GitHubAccount> FetchProfileAsync(string token, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await Http.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var root = doc.RootElement;

            return new GitHubAccount(
                root.GetProperty("login").GetString()!,
                root.TryGetProperty("name", out var name) ? name.GetString() : null,
                root.TryGetProperty("avatar_url", out var avatar) ? avatar.GetString() : null);
        }
        catch (HttpRequestException)
        {
            throw new GitHubNetworkException(NetworkMessage);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new GitHubNetworkException(NetworkMessage);
        }
    }

    // ------------------------------------------------------------------
    // Compte mémorisé : pseudo, nom et avatar seulement (aucun jeton)
    // ------------------------------------------------------------------

    private static string AccountPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Kode", "account.json");

    public static GitHubAccount? LoadSaved()
    {
        try
        {
            return File.Exists(AccountPath)
                ? JsonSerializer.Deserialize<GitHubAccount>(File.ReadAllText(AccountPath))
                : null;
        }
        catch
        {
            return null; // fichier illisible : on considère que personne n'est connecté
        }
    }

    public static void Save(GitHubAccount account)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(AccountPath)!);
        File.WriteAllText(AccountPath, JsonSerializer.Serialize(account));
    }

    public static void SignOut()
    {
        try { if (File.Exists(AccountPath)) File.Delete(AccountPath); }
        catch { /* rien de grave : le fichier sera réécrasé à la prochaine connexion */ }
    }

    // ------------------------------------------------------------------
    // Outils
    // ------------------------------------------------------------------

    private static async Task<JsonDocument> PostFormAsync(
        string url, Dictionary<string, string> form, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await Http.PostAsync(url, new FormUrlEncodedContent(form), cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonDocument.Parse(json);
        }
        catch (HttpRequestException)
        {
            throw new GitHubNetworkException(NetworkMessage);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new GitHubNetworkException(NetworkMessage + " (délai dépassé)");
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("Réponse inattendue de GitHub.");
        }
    }

    private static string Describe(string? error) => error switch
    {
        "access_denied" => "Connexion refusée sur GitHub.",
        "expired_token" => "Le code a expiré. Recommence la connexion.",
        "device_flow_disabled" =>
            "Le Device Flow n'est pas activé : sur GitHub, ouvre ton application Kode (Developer settings → OAuth Apps) " +
            "et coche « Enable Device Flow ».",
        "incorrect_client_credentials" => "Le Client ID de Kode est incorrect.",
        _ => $"Erreur GitHub : {error}"
    };
}