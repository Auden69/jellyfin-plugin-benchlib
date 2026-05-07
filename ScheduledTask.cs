using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BenchlibPlugin.Configuration;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace BenchlibPlugin;

public class ScheduledTask : IScheduledTask
{
    private readonly LibraryScanner _scanner;
    private readonly BenchlibSender _sender;
    private readonly ILogger<ScheduledTask> _logger;

    public ScheduledTask(LibraryScanner scanner, BenchlibSender sender, ILogger<ScheduledTask> logger)
    {
        _scanner = scanner;
        _sender  = sender;
        _logger  = logger;
    }

    public string Name        => "BenchLib — Envoyer les statistiques";
    public string Description => "Scanne les bibliothèques sélectionnées et envoie les statistiques agrégées vers BenchLib. Aucun titre n'est transmis.";
    public string Category    => "BenchLib";
    public string Key         => "BenchlibSendStats";
    public bool   IsHidden    => false;
    public bool   IsEnabled   => true;
    public bool   IsLogged    => true;

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => new[]
    {
        new TaskTriggerInfo
        {
            Type           = TaskTriggerInfoType.DailyTrigger,
            TimeOfDayTicks = TimeSpan.FromHours(3).Ticks,
        }
    };

    private static readonly JsonSerializerOptions _prettyJson = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken ct)
    {
        var config = Plugin.Instance?.Configuration;
        if (config == null)
        {
            _logger.LogError("[BenchLib] Configuration introuvable — tâche annulée");
            return;
        }

        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            _logger.LogWarning("[BenchLib] Clé API non configurée — renseignez-la dans Administration > Plugins > BenchLib");
            return;
        }

        if (config.SelectedLibraries == null || config.SelectedLibraries.Count == 0)
        {
            _logger.LogWarning("[BenchLib] Aucune bibliothèque sélectionnée — configurez le plugin dans Administration > Plugins > BenchLib");
            return;
        }

        _logger.LogInformation("[BenchLib] Tâche démarrée — {Count} bibliothèque(s)", config.SelectedLibraries.Count);
        progress.Report(0);

        var byMediaType = config.SelectedLibraries
            .Select(s => s.Split('|'))
            .Where(parts => parts.Length == 2)
            .GroupBy(parts => parts[1])
            .ToList();

        var total     = byMediaType.Count;
        var completed = 0;
        var results   = new List<LastScanResult>();

        foreach (var group in byMediaType)
        {
            if (ct.IsCancellationRequested) { _logger.LogInformation("[BenchLib] Annulé"); return; }

            var mediaType  = group.Key;
            var libraryIds = group.Select(p => p[0]).ToList();

            try
            {
                _logger.LogInformation("[BenchLib] Scan {MediaType}...", mediaType);

                var payload    = await _scanner.ScanLibrariesAsync(libraryIds, mediaType, ct);
                var payloadJson = JsonSerializer.Serialize(payload, _prettyJson);
                var result     = await _sender.SendAsync(payload, config.ApiKey, config.BenchlibApiUrl, ct);

                var scanResult = new LastScanResult
                {
                    MediaType    = mediaType,
                    ScannedAt    = DateTime.UtcNow.ToString("o"),
                    PayloadJson  = payloadJson,
                };

                if (result.Success)
                {
                    scanResult.Score         = result.Response?.Score?.Global;
                    scanResult.Certification = result.Response?.Score?.Certification;
                    _logger.LogInformation("[BenchLib] {MediaType} ✓ — score {Score} ({Cert})",
                        mediaType, scanResult.Score, scanResult.Certification);
                }
                else
                {
                    scanResult.Certification = "ERREUR";
                    _logger.LogWarning("[BenchLib] {MediaType} ✗ — {Error}", mediaType, result.Error);
                }

                results.Add(scanResult);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "[BenchLib] Erreur scan {MediaType}", mediaType);
                results.Add(new LastScanResult { MediaType = mediaType, Certification = "ERREUR", ScannedAt = DateTime.UtcNow.ToString("o") });
            }

            completed++;
            progress.Report((double)completed / total * 100);
        }

        // Sauvegarde des résultats dans la config
        if (results.Count > 0 && Plugin.Instance != null)
        {
            Plugin.Instance.Configuration.LastScanResults = results;
            Plugin.Instance.SaveConfiguration();
        }

        progress.Report(100);
        _logger.LogInformation("[BenchLib] Tâche terminée");
    }
}