using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace IdeCSharp.Core.Assistant;

/// <summary>Assistant branché sur l'API Gemini de Google, réponse reçue au fil de l'eau.</summary>
public class GeminiAssistantService : IAssistantService
{
    // Si Google retire ce modèle (erreur 404), il suffit de changer ce nom
    private const string Model = "gemini-2.5-flash";

    // On n'envoie pas plus de ce nombre de caractères du fichier, pour rester rapide et gratuit
    private const int MaxFileChars = 30000;

    private const string SystemPrompt =
        "Tu es l'assistant de Kode, un éditeur de code pour étudiants. " +
        "Réponds en français, de façon courte et pédagogique. " +
        "Quand tu proposes du code, mets-le dans un bloc de code et explique-le brièvement.";

    private static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };

    // La clé est relue à chaque question : si l'utilisateur la change, c'est pris en compte tout de suite
    public bool IsConfigured => AssistantKeyStore.Load() is not null;

    public async IAsyncEnumerable<string> AskAsync(string question, string? fileName, string? fileContent,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        string? key = AssistantKeyStore.Load();
        if (key is null)
            throw new InvalidOperationException("Aucune clé API : clique sur « Clé API » pour en ajouter une.");

        // Le fichier ouvert sert de contexte à la question
        var prompt = new StringBuilder();
        if (fileName is not null && fileContent is not null)
        {
            string shown = fileContent.Length > MaxFileChars ? fileContent[..MaxFileChars] + "\n[… fichier tronqué]" : fileContent;
            prompt.AppendLine($"Fichier ouvert : {fileName}");
            prompt.AppendLine("```");
            prompt.AppendLine(shown);
            prompt.AppendLine("```");
            prompt.AppendLine();
        }
        prompt.Append(question);

        string body = JsonSerializer.Serialize(new
        {
            systemInstruction = new { parts = new[] { new { text = SystemPrompt } } },
            contents = new[] { new { role = "user", parts = new[] { new { text = prompt.ToString() } } } }
        });

        string url = $"https://generativelanguage.googleapis.com/v1beta/models/{Model}:streamGenerateContent?alt=sse";
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        // La clé voyage dans un en-tête, pas dans l'URL
        request.Headers.Add("x-goog-api-key", key);

        HttpResponseMessage response;
        try
        {
            response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (HttpRequestException)
        {
            throw new InvalidOperationException("Impossible de joindre l'IA. Vérifie ta connexion internet.");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                // On lit la réponse de Google pour afficher la vraie cause de l'erreur
                string errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException(DescribeError(response.StatusCode, errorBody));
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            // Format SSE : chaque morceau arrive sur une ligne "data: {json}"
            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
            {
                if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;

                string text = ExtractText(line[5..].Trim());
                if (text.Length > 0) yield return text;
            }
        }
    }

    // Récupère le texte d'un morceau JSON ; un morceau inattendu est simplement ignoré
    private static string ExtractText(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
                return string.Empty;
            if (!candidates[0].TryGetProperty("content", out var content) || !content.TryGetProperty("parts", out var parts))
                return string.Empty;

            var sb = new StringBuilder();
            foreach (var part in parts.EnumerateArray())
                if (part.TryGetProperty("text", out var t)) sb.Append(t.GetString());
            return sb.ToString();
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static string DescribeError(HttpStatusCode status, string body)
    {
        string main = status switch
        {
            HttpStatusCode.BadRequest or HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized =>
                "Clé API refusée. Vérifie-la dans « Clé API ».",
            HttpStatusCode.NotFound => "Modèle introuvable : change le nom du modèle dans GeminiAssistantService.cs.",
            HttpStatusCode.TooManyRequests => "Trop de questions d'un coup (limite gratuite). Réessaie dans une minute.",
            _ => $"L'IA a répondu par une erreur ({(int)status}). Réessaie plus tard."
        };

        string detail = ExtractErrorMessage(body);
        return detail.Length > 0 ? $"{main} Détail Google : {detail}" : main;
    }

    // Récupère le champ error.message de la réponse JSON de Google
    private static string ExtractErrorMessage(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var message))
                return message.GetString() ?? string.Empty;
        }
        catch (JsonException) { /* corps illisible : on n'ajoute rien */ }
        return string.Empty;
    }
}