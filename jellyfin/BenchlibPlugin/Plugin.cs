using System;
using System.Collections.Generic;
using BenchlibPlugin.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace BenchlibPlugin;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public const string AgentVersion = "1.0.0";
    public static Plugin? Instance { get; private set; }

    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    public override string Name => "BenchLib";
    public override Guid Id => Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
    public override string Description => "Envoie les statistiques agrégées de vos bibliothèques vers BenchLib.";

    public IEnumerable<PluginPageInfo> GetPages()
    {
        var ns = GetType().Namespace;
        return new[]
        {
            new PluginPageInfo
            {
                Name = "BenchLib",
                EmbeddedResourcePath = $"{ns}.Configuration.configPage.html",
                EnableInMainMenu = false,
            },
            new PluginPageInfo
            {
                Name = "BenchLibJS",
                EmbeddedResourcePath = $"{ns}.Configuration.configPage.js",
                EnableInMainMenu = false,
            }
        };
    }
}