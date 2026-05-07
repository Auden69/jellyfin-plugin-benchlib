using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace BenchlibPlugin;

/// <summary>
/// Envoie le payload agrégé vers l'API BenchLib.
/// Gère les retries avec backoff exponentiel.
/// </summary>
public class BenchlibSender
{
    private readonly ILogger<BenchlibSender> _logger;
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30),
    };

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public BenchlibSender(ILogger<BenchlibSender> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Envoie le payload vers POST /api/v1/ingest avec retry x3 (backoff exponentiel).
    /// </summary>
    public async Task<SendResult> SendAsync(
        IngestPayload payload,
        string apiKey,
        string baseUrl,
        CancellationToken cancellationToken)
    {
        var url  = baseUrl.TrimEnd('/') + "/api/v1/ingest";
        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        var maxRetries = 3;

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("X-BenchLib-Key", apiKey);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                _logger.LogInformation(
                    "[BenchLib] Envoi {MediaType} — tentative {Attempt}/{Max}",
                    payload.MediaType, attempt, maxRetries);

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<IngestResponse>(body, _jsonOptions);
                    _logger.LogInformation(
                        "[BenchLib] ✓ {MediaType} envoyé — score global : {Score} ({Cert})",
                        payload.MediaType,
                        result?.Score?.Global,
                        result?.Score?.Certification);
                    return new SendResult { Success = true, Response = result };
                }

                // 401 / 403 — inutile de retenter
                if ((int)response.StatusCode is 401 or 403)
                {
                    _logger.LogError(
                        "[BenchLib] ✗ Authentification refusée ({Status}) — vérifiez votre clé API",
                        response.StatusCode);
                    return new SendResult { Success = false, Error = $"Auth refusée : {response.StatusCode}" };
                }

                // 429 — rate limit
                if ((int)response.StatusCode == 429)
                {
                    _logger.LogWarning("[BenchLib] Rate limit atteint — envoi annulé");
                    return new SendResult { Success = false, Error = "Rate limit" };
                }

                _logger.LogWarning(
                    "[BenchLib] ✗ Réponse {Status} — {Body}",
                    response.StatusCode, body[..Math.Min(body.Length, 200)]);
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("[BenchLib] Timeout (tentative {Attempt})", attempt);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning("[BenchLib] Erreur réseau (tentative {Attempt}) : {Msg}", attempt, ex.Message);
            }

            // Backoff exponentiel : 2s, 4s, 8s
            if (attempt < maxRetries)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                _logger.LogInformation("[BenchLib] Retry dans {Delay}s...", delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
        }

        return new SendResult { Success = false, Error = $"Échec après {maxRetries} tentatives" };
    }
}

// ─── Types réponse ────────────────────────────────────────────────────────────

public class SendResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public IngestResponse? Response { get; set; }
}

public class IngestResponse
{
    public bool Success { get; set; }
    public ScoreResponse? Score { get; set; }
}

public class ScoreResponse
{
    public double Global { get; set; }
    public string? Certification { get; set; }
    public double? Quality { get; set; }
    public double? Quantity { get; set; }
    public double? Availability { get; set; }
    public double? Freshness { get; set; }
    public double? Completeness { get; set; }
}
