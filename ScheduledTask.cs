using System;
using System.Collections.Generic;
using System.Linq;
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
    public string Description => "Scanne les bibliothèques activées et envoie les statistiques agrégées vers BenchLib. Aucun titre n'est transmis.";
    public string Category    => "BenchLib";
    public string Key         => "BenchlibSendStats";
    public bool   IsHidden    => false;
    public bool   IsEnabled   => true;
    public bool   IsLogged    => true;

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => new[]
    {
        new TaskTriggerInfo
        {
            Type = TaskTriggerInfoType.DailyTrigger,
            TimeOfDayTicks = TimeSpan.FromHours(3).Ticks,
        }
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

        _logger.LogInformation("[BenchLib] Tâche démarrée");
        progress.Report(0);

        var tasks = new List<(string Name, bool Enabled, Func<CancellationToken, Task<IngestPayload>> Fn)>
        {
            ("Films",   config.EnableMovies, _scanner.ScanMoviesAsync),
            ("Séries",  config.EnableSeries, _scanner.ScanSeriesAsync),
            ("Musique", config.EnableMusic,  _scanner.ScanMusicAsync),
        };

        var total     = tasks.Count(t => t.Enabled);
        var completed = 0;

        foreach (var (name, enabled, fn) in tasks)
        {
            if (!enabled) { _logger.LogInformation("[BenchLib] {Name} — désactivé", name); continue; }
            if (ct.IsCancellationRequested) { _logger.LogInformation("[BenchLib] Annulé"); return; }

            try
            {
                var payload = await fn(ct);
                var result  = await _sender.SendAsync(payload, config.ApiKey, config.BenchlibApiUrl, ct);

                if (result.Success)
                    _logger.LogInformation("[BenchLib] {Name} ✓ — score {Score} ({Cert})",
                        name, result.Response?.Score?.Global, result.Response?.Score?.Certification);
                else
                    _logger.LogWarning("[BenchLib] {Name} ✗ — {Error}", name, result.Error);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "[BenchLib] Erreur scan {Name}", name);
            }

            completed++;
            progress.Report((double)completed / total * 100);
        }

        progress.Report(100);
        _logger.LogInformation("[BenchLib] Tâche terminée");
    }
}