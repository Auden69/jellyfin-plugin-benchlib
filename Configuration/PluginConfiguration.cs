using MediaBrowser.Model.Plugins;

namespace BenchlibPlugin.Configuration;

/// <summary>Résultat du dernier envoi pour une bibliothèque.</summary>
public class LastScanResult
{
    public string MediaType      { get; set; } = string.Empty;
    public double? Score         { get; set; }
    public string? Certification { get; set; }
    public string? ScannedAt     { get; set; }
    public string? PayloadJson   { get; set; }
}

public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>Clé API BenchLib générée depuis le tableau de bord benchlib.com.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>URL de l'API BenchLib.</summary>
    public string BenchlibApiUrl { get; set; } = "https://api.benchlib.com";

    /// <summary>
    /// Bibliothèques sélectionnées par l'admin.
    /// Format : "libraryId|mediaType" ex: "abc123|MOVIES"
    /// </summary>
    public List<string> SelectedLibraries { get; set; } = new();

    /// <summary>Résultats des derniers envois par bibliothèque.</summary>
    public List<LastScanResult> LastScanResults { get; set; } = new();
}