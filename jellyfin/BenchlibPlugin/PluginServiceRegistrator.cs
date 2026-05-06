using BenchlibPlugin;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Enregistre les services du plugin dans le conteneur DI de Jellyfin.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<LibraryScanner>();
        serviceCollection.AddSingleton<BenchlibSender>();
        serviceCollection.AddSingleton<ScheduledTask>();
    }
}
