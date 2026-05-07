using System.Text.Json.Serialization;

namespace BenchlibPlugin;

public class IngestPayload
{
    [JsonPropertyName("agentVersion")]  public string AgentVersion { get; set; } = "1.0.0";
    [JsonPropertyName("serviceType")]   public string ServiceType  { get; set; } = "JELLYFIN";
    [JsonPropertyName("mediaType")]     public string MediaType    { get; set; } = string.Empty;
    [JsonPropertyName("scannedAt")]     public string ScannedAt    { get; set; } = string.Empty;
    [JsonPropertyName("apiResponseMs")] public int?   ApiResponseMs { get; set; }
    [JsonPropertyName("stats")]         public StatsPayload Stats  { get; set; } = new();
}

public class StatsPayload
{
    [JsonPropertyName("totalItems")]      public int TotalItems { get; set; }
    [JsonPropertyName("video")]           public VideoStats?    Video     { get; set; }
    [JsonPropertyName("audio")]           public AudioStats?    Audio     { get; set; }
    [JsonPropertyName("subtitles")]       public SubtitleStats? Subtitles { get; set; }
    [JsonPropertyName("metadata")]        public MetadataStats? Metadata  { get; set; }
    [JsonPropertyName("freshness")]       public FreshnessStats? Freshness { get; set; }
    [JsonPropertyName("series")]          public SeriesStats?   Series    { get; set; }
    [JsonPropertyName("music")]           public MusicStats?    Music     { get; set; }
    [JsonPropertyName("books")]           public object? Books    { get; set; } = null;
    [JsonPropertyName("audiobook")]       public object? Audiobook { get; set; } = null;
    [JsonPropertyName("languages")]       public List<LanguageStat>?        Languages       { get; set; }
    [JsonPropertyName("ageDistribution")] public Dictionary<string, int>?   AgeDistribution { get; set; }
}

public class VideoStats
{
    [JsonPropertyName("items4kDv")]       public int? Items4kDv       { get; set; }
    [JsonPropertyName("items4kHdr")]      public int? Items4kHdr      { get; set; }
    [JsonPropertyName("items4k")]         public int? Items4k         { get; set; }
    [JsonPropertyName("items1080p")]      public int? Items1080p      { get; set; }
    [JsonPropertyName("items720p")]       public int? Items720p       { get; set; }
    [JsonPropertyName("itemsSd")]         public int? ItemsSd         { get; set; }
    [JsonPropertyName("avgBitrateMbps")]  public double? AvgBitrateMbps { get; set; }
    [JsonPropertyName("itemsHevc")]       public int? ItemsHevc       { get; set; }
    [JsonPropertyName("itemsH264")]       public int? ItemsH264       { get; set; }
    [JsonPropertyName("itemsAv1")]        public int? ItemsAv1        { get; set; }
}

public class AudioStats
{
    [JsonPropertyName("itemsAtmos")]      public int? ItemsAtmos   { get; set; }
    [JsonPropertyName("itemsTrueHd")]     public int? ItemsTrueHd  { get; set; }
    [JsonPropertyName("itemsDtsHd")]      public int? ItemsDtsHd   { get; set; }
    [JsonPropertyName("itemsDts")]        public int? ItemsDts     { get; set; }
    [JsonPropertyName("itemsAc3")]        public int? ItemsAc3     { get; set; }
    [JsonPropertyName("itemsStereo")]     public int? ItemsStereo  { get; set; }
    [JsonPropertyName("itemsFlac")]       public int? ItemsFlac    { get; set; }
    [JsonPropertyName("itemsMp3320")]     public int? ItemsMp3320  { get; set; }
    [JsonPropertyName("itemsMp3256")]     public int? ItemsMp3256  { get; set; }
    [JsonPropertyName("itemsMp3192")]     public int? ItemsMp3192  { get; set; }
    [JsonPropertyName("itemsMp3Low")]     public int? ItemsMp3Low  { get; set; }
    [JsonPropertyName("itemsAac")]        public int? ItemsAac     { get; set; }
    [JsonPropertyName("itemsM4b")]        public int? ItemsM4b     { get; set; }
    [JsonPropertyName("avgAudioBitrate")] public int? AvgAudioBitrate { get; set; }
}

public class SubtitleStats
{
    [JsonPropertyName("subtitleTracksTotal")] public int? SubtitleTracksTotal { get; set; }
    [JsonPropertyName("itemsWithSubtitles")]  public int? ItemsWithSubtitles  { get; set; }
}

public class MetadataStats
{
    [JsonPropertyName("itemsWithPoster")]   public int? ItemsWithPoster   { get; set; }
    [JsonPropertyName("itemsWithSynopsis")] public int? ItemsWithSynopsis { get; set; }
    [JsonPropertyName("itemsWithCasting")]  public int? ItemsWithCasting  { get; set; }
    [JsonPropertyName("itemsWithGenre")]    public int? ItemsWithGenre    { get; set; }
}

public class FreshnessStats
{
    [JsonPropertyName("itemsAddedLast7Days")]  public int?    ItemsAddedLast7Days  { get; set; }
    [JsonPropertyName("itemsAddedLast30Days")] public int?    ItemsAddedLast30Days { get; set; }
    [JsonPropertyName("lastAddedAt")]          public string? LastAddedAt          { get; set; }
    [JsonPropertyName("avgDaysToAdd")]         public double? AvgDaysToAdd         { get; set; }
}

public class SeriesStats
{
    [JsonPropertyName("totalSeries")]       public int?    TotalSeries       { get; set; }
    [JsonPropertyName("totalSeasons")]      public int?    TotalSeasons      { get; set; }
    [JsonPropertyName("totalEpisodes")]     public int?    TotalEpisodes     { get; set; }
    [JsonPropertyName("seriesResolved")]    public int?    SeriesResolved    { get; set; }
    [JsonPropertyName("episodesPresent")]   public int?    EpisodesPresent   { get; set; }
    [JsonPropertyName("episodesReference")] public int?    EpisodesReference { get; set; }
    [JsonPropertyName("completenessRatio")] public double? CompletenessRatio { get; set; }
}

public class MusicStats
{
    [JsonPropertyName("totalAlbums")]      public int? TotalAlbums      { get; set; }
    [JsonPropertyName("totalArtists")]     public int? TotalArtists     { get; set; }
    [JsonPropertyName("itemsWithCover")]   public int? ItemsWithCover   { get; set; }
    [JsonPropertyName("itemsWithId3Tags")] public int? ItemsWithId3Tags { get; set; }
}

public class LanguageStat
{
    [JsonPropertyName("trackType")] public string TrackType { get; set; } = string.Empty;
    [JsonPropertyName("language")]  public string Language  { get; set; } = string.Empty;
    [JsonPropertyName("count")]     public int    Count     { get; set; }
}