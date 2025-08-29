using System;
using Dalamud.Interface.Components;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using Dalamud.Bindings.ImGui;
using TLink.Core.Module;
using TLink.Modules.Translation.Services;
using TLink.Utils;

namespace TLink.Modules.Translation.Providers.DeepL;

[ModuleInfo("DeepL", "1.0.0", Dependencies = ["Translation"], Description = "DeepL translation provider for high-quality translations")]
public class DeepLModule : ModuleBase
{
    private DeepLConfig? moduleConfig;
    private DeepLPipelineHandler? pipelineHandler;
    
    public override string Name => "DeepL";
    public override string Version => "1.0.0";
    public override string[] Dependencies => ["Translation"];
    
    // 1. Use proper lifecycle to load configuration
    protected override void LoadConfiguration()
    {
        moduleConfig = GetModuleConfig<DeepLConfig>();
    }
    
    // 2. Delegate dependency creation to a DI container using a factory pattern
    public override void RegisterServices(IServiceCollection services)
    {
        // Do NOT register moduleConfig - it's null at this point
        // Use a factory pattern to defer creation until Initialize phase
        
        services.AddSingleton<DeepLApiClient>(provider => 
            new DeepLApiClient(
                this.moduleConfig!, 
                provider.GetRequiredService<IPluginLog>()
            )
        );
        
        services.AddSingleton<DeepLPipelineHandler>(provider =>
            new DeepLPipelineHandler(
                provider.GetRequiredService<DeepLApiClient>(),
                this.moduleConfig!,
                provider.GetRequiredService<IPluginLog>(),
                provider.GetRequiredService<SeStringProcessor>()
            )
        );
    }
    
    // 3. Get required services through dependency injection
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
            Logger.Warning("DeepL handler is not configured or disabled, skipping registration. Please provide an API Key in the settings.");
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
        GC.SuppressFinalize(this);
    }
    
    // 4. Module must provide a UI to control its configuration
    public override void DrawConfiguration()
    {
        if (moduleConfig == null) return;

        var configChanged = false;

        ImGui.Text("DeepL API Settings");
        ImGui.Separator();

        var tempApiKey = moduleConfig.ApiKey;
        if (ImGui.InputText("DeepL API Key", ref tempApiKey, 100, ImGuiInputTextFlags.Password))
        {
            moduleConfig.ApiKey = tempApiKey;
            configChanged = true;
        }

        var isEnabled = moduleConfig.IsEnabled;
        if (ImGui.Checkbox("Enable DeepL Handler", ref isEnabled))
        {
            moduleConfig.IsEnabled = isEnabled;
            configChanged = true;
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker("Enables the DeepL provider in the translation pipeline.\nA valid API Key is required. A plugin reload might be needed to apply changes.");

        var usePro = moduleConfig.UsePro;
        if (ImGui.Checkbox("Use Pro API URL", ref usePro))
        {
            moduleConfig.UsePro = usePro;
            configChanged = true;
        }

        if (configChanged)
        {
            SaveConfiguration();
        }
    }
}
