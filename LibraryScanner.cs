using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace BenchlibPlugin;

public class LibraryScanner
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<LibraryScanner> _logger;

    public LibraryScanner(ILibraryManager libraryManager, ILogger<LibraryScanner> logger)
    {
        _libraryManager = libraryManager;
        _logger = logger;
    }


    // ─── Dispatcher principal ─────────────────────────────────────────────────

    public Task<IngestPayload> ScanLibrariesAsync(List<string> libraryIds, string mediaType, CancellationToken ct)
    {
        return mediaType switch
        {
            "MOVIES" => ScanMoviesAsync(ct, libraryIds),
            "SERIES" => ScanSeriesAsync(ct, libraryIds),
            "MUSIC"  => ScanMusicAsync(ct, libraryIds),
            _         => Task.FromResult(new IngestPayload
            {
                MediaType = mediaType,
                ScannedAt = DateTime.UtcNow.ToString("o"),
                Stats     = new StatsPayload { TotalItems = 0 }
            })
        };
    }

    // ─── Films ────────────────────────────────────────────────────────────────

    public Task<IngestPayload> ScanMoviesAsync(CancellationToken ct, List<string>? libraryIds = null)

    {
        _logger.LogInformation("[BenchLib] Scan Films démarré");
        var startMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var moviesQuery = new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.Movie },
            IsVirtualItem    = false,
            Recursive        = true,
        };
        if (libraryIds != null && libraryIds.Count > 0)
            moviesQuery.AncestorIds = libraryIds.Select(id => Guid.Parse(id)).ToArray();
        var movies = _libraryManager.GetItemList(moviesQuery);

        var video     = new VideoStats();
        var audio     = new AudioStats();
        var subtitles = new SubtitleStats();
        var metadata  = new MetadataStats();
        var audioLangs    = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var subtitleLangs = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var ageDist       = new Dictionary<string, int>();
        var bitrateSum = 0L; var bitrateCount = 0;
        DateTime? lastAdded = null;
        var cutoff30 = DateTime.UtcNow.AddDays(-30);
        var cutoff7  = DateTime.UtcNow.AddDays(-7);
        var added30  = 0; var added7 = 0;

        foreach (var item in movies)
        {
            if (ct.IsCancellationRequested) break;

            var vs = item.GetMediaStreams().FirstOrDefault(s => s.Type == MediaStreamType.Video);
            if (vs != null)
            {
                var h = vs.Height ?? 0;
                var dv  = IsDv(vs); var hdr = IsHdr(vs);
                if      (dv  && h >= 2160) video.Items4kDv  = (video.Items4kDv  ?? 0) + 1;
                else if (hdr && h >= 2160) video.Items4kHdr = (video.Items4kHdr ?? 0) + 1;
                else if (h >= 2160)        video.Items4k    = (video.Items4k    ?? 0) + 1;
                else if (h >= 1080)        video.Items1080p = (video.Items1080p ?? 0) + 1;
                else if (h >= 720)         video.Items720p  = (video.Items720p  ?? 0) + 1;
                else                       video.ItemsSd    = (video.ItemsSd    ?? 0) + 1;

                var c = (vs.Codec ?? "").ToLowerInvariant();
                if      (c is "hevc" or "h265") video.ItemsHevc = (video.ItemsHevc ?? 0) + 1;
                else if (c is "h264" or "avc")  video.ItemsH264 = (video.ItemsH264 ?? 0) + 1;
                else if (c == "av1")            video.ItemsAv1  = (video.ItemsAv1  ?? 0) + 1;

                if (vs.BitRate.HasValue) { bitrateSum += vs.BitRate.Value; bitrateCount++; }
            }

            var best = BestAudio(item.GetMediaStreams());
            if (best != null) { ClassifyAudio(best, audio); AddLang(best.Language, audioLangs); }
            foreach (var s in item.GetMediaStreams().Where(s => s.Type == MediaStreamType.Audio))
                AddLang(s.Language, audioLangs);

            var subs = item.GetMediaStreams().Where(s => s.Type == MediaStreamType.Subtitle).ToList();
            subtitles.SubtitleTracksTotal = (subtitles.SubtitleTracksTotal ?? 0) + subs.Count;
            if (subs.Count > 0) subtitles.ItemsWithSubtitles = (subtitles.ItemsWithSubtitles ?? 0) + 1;
            foreach (var s in subs) AddLang(s.Language, subtitleLangs);

            if (item.HasImage(ImageType.Primary))           metadata.ItemsWithPoster   = (metadata.ItemsWithPoster   ?? 0) + 1;
            if (!string.IsNullOrWhiteSpace(item.Overview))  metadata.ItemsWithSynopsis = (metadata.ItemsWithSynopsis ?? 0) + 1;
            if (item.Genres?.Length > 0)                    metadata.ItemsWithGenre    = (metadata.ItemsWithGenre    ?? 0) + 1;

            if (item.DateCreated > cutoff30) added30++;
            if (item.DateCreated > cutoff7)  added7++;
            if (lastAdded == null || item.DateCreated > lastAdded) lastAdded = item.DateCreated;
            if (item.ProductionYear.HasValue)
            {
                var y = item.ProductionYear.Value.ToString();
                ageDist[y] = ageDist.GetValueOrDefault(y) + 1;
            }
        }

        if (bitrateCount > 0)
            video.AvgBitrateMbps = Math.Round((double)bitrateSum / bitrateCount / 1_000_000, 2);

        var payload = new IngestPayload
        {
            MediaType     = "MOVIES",
            ScannedAt     = DateTime.UtcNow.ToString("o"),
            ApiResponseMs = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startMs),
            Stats = new StatsPayload
            {
                TotalItems      = movies.Count,
                Video           = video,
                Audio           = audio,
                Subtitles       = subtitles,
                Metadata        = metadata,
                Freshness       = new FreshnessStats { ItemsAddedLast30Days = added30, ItemsAddedLast7Days = added7, LastAddedAt = lastAdded?.ToString("o") },
                AgeDistribution = ageDist.Count > 0 ? ageDist : null,
                Languages       = LangStats(audioLangs, subtitleLangs),
            }
        };

        _logger.LogInformation("[BenchLib] Scan Films terminé — {Count} films", movies.Count);
        return Task.FromResult(payload);
    }

    // ─── Séries ───────────────────────────────────────────────────────────────

    public Task<IngestPayload> ScanSeriesAsync(CancellationToken ct, List<string>? libraryIds = null)
    {
        _logger.LogInformation("[BenchLib] Scan Séries démarré");
        var startMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var seriesQuery = new InternalItemsQuery { IncludeItemTypes = new[] { BaseItemKind.Series }, IsVirtualItem = false, Recursive = true };
        var episodesQuery = new InternalItemsQuery { IncludeItemTypes = new[] { BaseItemKind.Episode }, IsVirtualItem = false, Recursive = true };
        if (libraryIds != null && libraryIds.Count > 0)
        {
            var guids = libraryIds.Select(id => Guid.Parse(id)).ToArray();
            seriesQuery.AncestorIds   = guids;
            episodesQuery.AncestorIds = guids;
        }
        var allSeries = _libraryManager.GetItemList(seriesQuery).OfType<Series>().ToList();
        var episodes  = _libraryManager.GetItemList(episodesQuery).OfType<Episode>().ToList();

        var video     = new VideoStats();
        var audio     = new AudioStats();
        var subtitles = new SubtitleStats();
        var metadata  = new MetadataStats();
        var audioLangs    = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var subtitleLangs = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var ageDist   = new Dictionary<string, int>();
        var cutoff30  = DateTime.UtcNow.AddDays(-30);
        var cutoff7   = DateTime.UtcNow.AddDays(-7);
        var added30   = 0; var added7 = 0;
        DateTime? lastAdded = null;

        foreach (var ep in episodes)
        {
            if (ct.IsCancellationRequested) break;

            var vs = ep.GetMediaStreams().FirstOrDefault(s => s.Type == MediaStreamType.Video);
            if (vs != null)
            {
                var h = vs.Height ?? 0;
                var dv = IsDv(vs); var hdr = IsHdr(vs);
                if      (dv  && h >= 2160) video.Items4kDv  = (video.Items4kDv  ?? 0) + 1;
                else if (hdr && h >= 2160) video.Items4kHdr = (video.Items4kHdr ?? 0) + 1;
                else if (h >= 2160)        video.Items4k    = (video.Items4k    ?? 0) + 1;
                else if (h >= 1080)        video.Items1080p = (video.Items1080p ?? 0) + 1;
                else if (h >= 720)         video.Items720p  = (video.Items720p  ?? 0) + 1;
                else                       video.ItemsSd    = (video.ItemsSd    ?? 0) + 1;

                var c = (vs.Codec ?? "").ToLowerInvariant();
                if      (c is "hevc" or "h265") video.ItemsHevc = (video.ItemsHevc ?? 0) + 1;
                else if (c is "h264" or "avc")  video.ItemsH264 = (video.ItemsH264 ?? 0) + 1;
                else if (c == "av1")            video.ItemsAv1  = (video.ItemsAv1  ?? 0) + 1;
            }

            var best = BestAudio(ep.GetMediaStreams());
            if (best != null) { ClassifyAudio(best, audio); AddLang(best.Language, audioLangs); }

            var subs = ep.GetMediaStreams().Where(s => s.Type == MediaStreamType.Subtitle).ToList();
            subtitles.SubtitleTracksTotal = (subtitles.SubtitleTracksTotal ?? 0) + subs.Count;
            if (subs.Count > 0) subtitles.ItemsWithSubtitles = (subtitles.ItemsWithSubtitles ?? 0) + 1;
            foreach (var s in subs) AddLang(s.Language, subtitleLangs);

            if (ep.DateCreated > cutoff30) added30++;
            if (ep.DateCreated > cutoff7)  added7++;
            if (lastAdded == null || ep.DateCreated > lastAdded) lastAdded = ep.DateCreated;
        }

        foreach (var series in allSeries)
        {
            if (series.HasImage(ImageType.Primary))           metadata.ItemsWithPoster   = (metadata.ItemsWithPoster   ?? 0) + 1;
            if (!string.IsNullOrWhiteSpace(series.Overview))  metadata.ItemsWithSynopsis = (metadata.ItemsWithSynopsis ?? 0) + 1;
            if (series.Genres?.Length > 0)                    metadata.ItemsWithGenre    = (metadata.ItemsWithGenre    ?? 0) + 1;
            if (series.ProductionYear.HasValue)
            {
                var y = series.ProductionYear.Value.ToString();
                ageDist[y] = ageDist.GetValueOrDefault(y) + 1;
            }
        }

        var seriesStats = CalcCompleteness(allSeries, ct);
        var totalSeasons = allSeries.Sum(s => _libraryManager.GetItemList(new InternalItemsQuery
        {
            ParentId         = s.Id,
            IncludeItemTypes = new[] { BaseItemKind.Season },
            IsVirtualItem    = false,
        }).Count);

        seriesStats.TotalSeries   = allSeries.Count;
        seriesStats.TotalSeasons  = totalSeasons;
        seriesStats.TotalEpisodes = episodes.Count;

        var payload = new IngestPayload
        {
            MediaType     = "SERIES",
            ScannedAt     = DateTime.UtcNow.ToString("o"),
            ApiResponseMs = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startMs),
            Stats = new StatsPayload
            {
                TotalItems      = allSeries.Count,
                Video           = video,
                Audio           = audio,
                Subtitles       = subtitles,
                Metadata        = metadata,
                Freshness       = new FreshnessStats { ItemsAddedLast30Days = added30, ItemsAddedLast7Days = added7, LastAddedAt = lastAdded?.ToString("o") },
                Series          = seriesStats,
                AgeDistribution = ageDist.Count > 0 ? ageDist : null,
                Languages       = LangStats(audioLangs, subtitleLangs),
            }
        };

        _logger.LogInformation("[BenchLib] Scan Séries terminé — {S} séries, {E} épisodes", allSeries.Count, episodes.Count);
        return Task.FromResult(payload);
    }

    // ─── Musique ──────────────────────────────────────────────────────────────

    public Task<IngestPayload> ScanMusicAsync(CancellationToken ct, List<string>? libraryIds = null)
    {
        _logger.LogInformation("[BenchLib] Scan Musique démarré");
        var startMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var tracksQuery = new InternalItemsQuery { IncludeItemTypes = new[] { BaseItemKind.Audio }, IsVirtualItem = false, Recursive = true };
        var albumsQuery = new InternalItemsQuery { IncludeItemTypes = new[] { BaseItemKind.MusicAlbum }, IsVirtualItem = false, Recursive = true };
        if (libraryIds != null && libraryIds.Count > 0)
        {
            var guids = libraryIds.Select(id => Guid.Parse(id)).ToArray();
            tracksQuery.AncestorIds = guids;
            albumsQuery.AncestorIds = guids;
        }
        var tracks = _libraryManager.GetItemList(tracksQuery).OfType<Audio>().ToList();
        var albums = _libraryManager.GetItemList(albumsQuery);

        var artists  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var audioSt  = new AudioStats();
        var cutoff30 = DateTime.UtcNow.AddDays(-30);
        var cutoff7  = DateTime.UtcNow.AddDays(-7);
        var added30  = 0; var added7 = 0;
        var withCover = 0; var withId3 = 0;
        DateTime? lastAdded = null;

        foreach (var track in tracks)
        {
            if (ct.IsCancellationRequested) break;

            var s = track.GetMediaStreams().FirstOrDefault(x => x.Type == MediaStreamType.Audio);
            // Codec depuis le stream audio, avec fallback sur le container du fichier.
            // Jellyfin peut retourner un stream avec Codec null/vide pour certains formats
            // (ogg, opus, wma, wav, etc.) — dans ce cas on lit track.Container.
            var codec = (s?.Codec ?? "").ToLowerInvariant();
            if (string.IsNullOrEmpty(codec))
                codec = (track.Container ?? "").ToLowerInvariant();

            if (codec == "flac") audioSt.ItemsFlac = (audioSt.ItemsFlac ?? 0) + 1;
            else if (codec == "mp3")
            {
                var br = s?.BitRate ?? 0;
                if      (br >= 300_000) audioSt.ItemsMp3320 = (audioSt.ItemsMp3320 ?? 0) + 1;
                else if (br >= 240_000) audioSt.ItemsMp3256 = (audioSt.ItemsMp3256 ?? 0) + 1;
                else if (br >= 180_000) audioSt.ItemsMp3192 = (audioSt.ItemsMp3192 ?? 0) + 1;
                else                   audioSt.ItemsMp3Low  = (audioSt.ItemsMp3Low  ?? 0) + 1;
            }
            else if (codec is "aac" or "m4a" or "m4b" or "alac") audioSt.ItemsAac = (audioSt.ItemsAac ?? 0) + 1;
            // ogg/opus/vorbis → comptés dans itemsStereo (pas de champ dédié)
            // wma/wav/aiff → non scorés, ignorés volontairement

            if (track.HasImage(ImageType.Primary)) withCover++;
            if (!string.IsNullOrWhiteSpace(track.Name) && !string.IsNullOrWhiteSpace(track.Album)) withId3++;
            if (track.AlbumArtists != null) foreach (var a in track.AlbumArtists) artists.Add(a);

            if (track.DateCreated > cutoff30) added30++;
            if (track.DateCreated > cutoff7)  added7++;
            if (lastAdded == null || track.DateCreated > lastAdded) lastAdded = track.DateCreated;
        }

        var payload = new IngestPayload
        {
            MediaType     = "MUSIC",
            ScannedAt     = DateTime.UtcNow.ToString("o"),
            ApiResponseMs = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startMs),
            Stats = new StatsPayload
            {
                TotalItems = tracks.Count,
                Audio      = audioSt,
                Music      = new MusicStats { TotalAlbums = albums.Count, TotalArtists = artists.Count, ItemsWithCover = withCover, ItemsWithId3Tags = withId3 },
                Freshness  = new FreshnessStats { ItemsAddedLast30Days = added30, ItemsAddedLast7Days = added7, LastAddedAt = lastAdded?.ToString("o") },
            }
        };

        _logger.LogInformation("[BenchLib] Scan Musique terminé — {Count} pistes", tracks.Count);
        return Task.FromResult(payload);
    }

    // ─── Completeness (Option C) ──────────────────────────────────────────────

    private SeriesStats CalcCompleteness(List<Series> allSeries, CancellationToken ct)
    {
        var present = 0; var reference = 0; var resolved = 0;

        foreach (var series in allSeries)
        {
            if (ct.IsCancellationRequested) break;
            if (series.ProviderIds?.ContainsKey("Tmdb") != true &&
                series.ProviderIds?.ContainsKey("Tvdb") != true) continue;

            var isEnded      = series.Status == SeriesStatus.Ended;
            var isContinuing = series.Status == SeriesStatus.Continuing;
            if (!isEnded && !isContinuing) continue;

            var seasons = _libraryManager.GetItemList(new InternalItemsQuery
            {
                ParentId         = series.Id,
                IncludeItemTypes = new[] { BaseItemKind.Season },
                IsVirtualItem    = false,
            }).OfType<Season>().ToList();

            var contributed = false;
            foreach (var season in seasons)
            {
                if (season.IndexNumber == 0) continue;
                if (isContinuing && IsInProgress(season, seasons)) continue;

                var allEps  = season.Children.OfType<Episode>().ToList();
                var exp     = allEps.Count;
                var pres    = allEps.Count(e => !e.IsVirtualItem);
                if (exp <= 0) continue;

                present   += pres;
                reference += exp;
                contributed = true;
            }
            if (contributed) resolved++;
        }

        return new SeriesStats
        {
            SeriesResolved    = resolved,
            EpisodesPresent   = present,
            EpisodesReference = reference,
            CompletenessRatio = reference > 0 ? Math.Round((double)present / reference, 4) : null,
        };
    }

    private static bool IsInProgress(Season season, List<Season> all)
    {
        var max = all.Where(s => s.IndexNumber > 0).Max(s => s.IndexNumber ?? 0);
        if (season.IndexNumber < max) return false;
        return season.Children.OfType<Episode>().Any(e => e.IsVirtualItem);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static MediaStream? BestAudio(IEnumerable<MediaStream> streams)
    {
        var list = streams.Where(s => s.Type == MediaStreamType.Audio).ToList();
        return list.Count == 0 ? null : list.OrderByDescending(AudioScore).First();
    }

    private static int AudioScore(MediaStream s)
    {
        var c = (s.Codec ?? "").ToLowerInvariant();
        var p = (s.Profile ?? "").ToLowerInvariant();
        var t = (s.Title ?? "").ToLowerInvariant();
        var atmos = t.Contains("atmos");
        if (c == "truehd" && atmos) return 100;
        if (c == "eac3"   && atmos) return 95;
        if (c == "truehd")          return 90;
        if (c == "dts" && p.Contains("ma")) return 85;
        if (c == "dts" && p.Contains("x"))  return 83;
        if (c == "dts")             return 70;
        if (c is "eac3" or "ac3")   return 50;
        return 20;
    }

    private static void ClassifyAudio(MediaStream s, AudioStats a)
    {
        var c = (s.Codec ?? "").ToLowerInvariant();
        var p = (s.Profile ?? "").ToLowerInvariant();
        var t = (s.Title ?? "").ToLowerInvariant();
        var atmos = t.Contains("atmos");
        if      (c == "truehd" && atmos)         a.ItemsAtmos  = (a.ItemsAtmos  ?? 0) + 1;
        else if (c == "eac3"   && atmos)         a.ItemsAtmos  = (a.ItemsAtmos  ?? 0) + 1;
        else if (c == "truehd")                  a.ItemsTrueHd = (a.ItemsTrueHd ?? 0) + 1;
        else if (c == "dts" && p.Contains("ma")) a.ItemsDtsHd  = (a.ItemsDtsHd  ?? 0) + 1;
        else if (c == "dts" && p.Contains("x"))  a.ItemsDtsHd  = (a.ItemsDtsHd  ?? 0) + 1;
        else if (c == "dts")                     a.ItemsDts    = (a.ItemsDts    ?? 0) + 1;
        else if (c is "eac3" or "ac3")           a.ItemsAc3    = (a.ItemsAc3    ?? 0) + 1;
        else                                     a.ItemsStereo = (a.ItemsStereo ?? 0) + 1;
    }

    private static bool IsDv(MediaStream s)
    {
        var t = (s.Title ?? "").ToLowerInvariant();
        var c = (s.Codec ?? "").ToLowerInvariant();
        return t.Contains("dolby vision") || t.Contains(" dv") || c.Contains("dvhe") || c.Contains("dvav");
    }

    private static bool IsHdr(MediaStream s)
    {
        var t  = (s.Title      ?? "").ToLowerInvariant();
        var cs = (s.ColorSpace ?? "").ToLowerInvariant();
        return t.Contains("hdr") || cs.Contains("bt2020") || cs.Contains("smpte2084");
    }

    private static void AddLang(string? lang, Dictionary<string, int> dict)
    {
        var l = NormLang(lang);
        if (l != null) dict[l] = dict.GetValueOrDefault(l) + 1;
    }

    private static string? NormLang(string? lang)
    {
        if (string.IsNullOrWhiteSpace(lang)) return null;
        return lang.ToLowerInvariant().Trim() switch
        {
            "fre" or "fra" => "fr",  "eng" => "en",
            "ger" or "deu" => "de",  "spa" => "es",
            "ita"          => "it",  "por" => "pt",
            "jpn"          => "ja",  "chi" or "zho" => "zh",
            "kor"          => "ko",  "rus" => "ru",  "ara" => "ar",
            var l          => l.Length <= 3 ? l : null,
        };
    }

    private static List<LanguageStat> LangStats(Dictionary<string, int> a, Dictionary<string, int> s)
    {
        var r = new List<LanguageStat>();
        foreach (var (l, c) in a) r.Add(new LanguageStat { TrackType = "AUDIO",    Language = l, Count = c });
        foreach (var (l, c) in s) r.Add(new LanguageStat { TrackType = "SUBTITLE", Language = l, Count = c });
        return r;
    }
}