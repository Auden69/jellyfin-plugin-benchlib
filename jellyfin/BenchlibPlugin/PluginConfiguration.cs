using MediaBrowser.Model.Plugins;

namespace BenchlibPlugin.Configuration;

/// <summary>
/// Configuration du plugin BenchLib.
/// Saisie par l'administrateur dans l'interface Jellyfin.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Clé API BenchLib générée depuis le tableau de bord benchlib.com.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// URL de l'API BenchLib.
    /// Modifiable pour les environnements de test ou auto-hébergés.
    /// </summary>
    public string BenchlibApiUrl { get; set; } = "https://api.benchlib.com";

    /// <summary>
    /// Activer l'envoi des statistiques Films.
    /// </summary>
    public bool EnableMovies { get; set; } = true;

    /// <summary>
    /// Activer l'envoi des statistiques Séries.
    /// </summary>
    public bool EnableSeries { get; set; } = true;

    /// <summary>
    /// Activer l'envoi des statistiques Musique.
    /// </summary>
    public bool EnableMusic { get; set; } = true;

    /// <summary>
    /// Niveau de log souhaité (Info, Debug).
    /// </summary>
    public string LogLevel { get; set; } = "Info";
}
