using Microsoft.Extensions.DependencyInjection;
using TLink.Core.Configuration;
using TLink.Core.Module;
using TLink.Modules.Translation.Services;

namespace TLink.Modules.Translation.Providers.DeepL;

[ModuleInfo("DeepL", "1.0.0", Dependencies = ["Translation"], Description = "DeepL translation provider for high-quality translations")]
public class DeepLModule : ModuleBase
{
    private DeepLPipelineHandler? pipelineHandler;
    
    public override string Name => "DeepL";
    public override string Version => "1.0.0";
    public override string[] Dependencies => ["Translation"];
    
    public override void RegisterServices(IServiceCollection services)
    {
        // Register config as a factory since it's not available during RegisterServices
        services.AddSingleton<DeepLConfig>(sp =>
        {
            var pluginConfig = sp.GetRequiredService<PluginConfiguration>();
            var config = pluginConfig.GetModuleConfig("DeepL");
            if (config is DeepLConfig deepLConfig)
                return deepLConfig;
            
            // Create the default config if not found
            var newConfig = new DeepLConfig { ModuleName = "DeepL" };
            pluginConfig.SetModuleConfig("DeepL", newConfig);
            return newConfig;
        });
        
        services.AddSingleton<DeepLApiClient>(sp =>
        {
            var config = sp.GetRequiredService<DeepLConfig>();
            var logger = sp.GetRequiredService<Dalamud.Plugin.Services.IPluginLog>();
            return new DeepLApiClient(config, logger);
        });
        
        services.AddSingleton<DeepLPipelineHandler>(sp =>
        {
            var apiClient = sp.GetRequiredService<DeepLApiClient>();
            var config = sp.GetRequiredService<DeepLConfig>();
            var logger = sp.GetRequiredService<Dalamud.Plugin.Services.IPluginLog>();
            var seStringProcessor = sp.GetRequiredService<SeStringProcessor>();
            
            return new DeepLPipelineHandler(apiClient, config, logger, seStringProcessor);
        });
    }
    
    public override void Initialize()
    {
        var handlerRegistry = Services.GetRequiredService<IPipelineHandlerRegistry>();
        pipelineHandler = Services.GetRequiredService<DeepLPipelineHandler>();
        
        if (pipelineHandler.IsEnabled)
        {
            handlerRegistry.RegisterHandler(pipelineHandler);
            Logger.Information($"DeepL handler registered with priority {pipelineHandler.Priority}");
        }
        else
        {
            Logger.Warning("DeepL handler is not configured or disabled, skipping registration");
        }
    }
    
    public override void Dispose()
    {
        var handlerRegistry = Services.GetService<IPipelineHandlerRegistry>();
        
        if (pipelineHandler != null && handlerRegistry != null)
        {
            handlerRegistry.UnregisterHandler(pipelineHandler.Name);
            pipelineHandler.Dispose();
        }
        
        base.Dispose();
    }
}
