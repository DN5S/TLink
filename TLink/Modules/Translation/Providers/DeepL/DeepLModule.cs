using System;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using Dalamud.Bindings.ImGui;
using TLink.Core.Module;
using TLink.Core.UI;
using TLink.Modules.Translation.Services;
using TLink.Utils;

namespace TLink.Modules.Translation.Providers.DeepL;

[ModuleInfo("DeepL", "1.0.0", Dependencies = ["Translation"], Description = "DeepL translation provider")]
public class DeepLModule : ModuleBase
{
    private DeepLConfig? moduleConfig;
    private DeepLPipelineHandler? pipelineHandler;
    private IFramework? framework;
    
    // API key validation state management
    private enum ApiKeyStatus { Idle, Validating, Valid, Invalid }
    private ApiKeyStatus currentApiKeyStatus = ApiKeyStatus.Idle;
    private string lastValidatedApiKey = string.Empty;
    
    public override string Name => "DeepL";
    public override string Version => "1.0.1";
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
        framework = Services.GetRequiredService<IFramework>();
        var handlerRegistry = Services.GetRequiredService<IPipelineHandlerRegistry>();
        pipelineHandler = Services.GetRequiredService<DeepLPipelineHandler>();
        
        // Register or unregister based on the current state
        UpdateHandlerRegistration(handlerRegistry);
        
        // Subscribe to configuration changes for dynamic updates
        Subscriptions.Add(
            EventBus.Listen<DeepLConfigChanged>()
                .Subscribe(_ => UpdateHandlerRegistration(handlerRegistry))
        );
    }
    
    private void UpdateHandlerRegistration(IPipelineHandlerRegistry handlerRegistry)
    {
        if (pipelineHandler == null) 
        {
            Logger.Warning("DeepL: pipelineHandler is null, cannot register");
            return;
        }
        
        Logger.Information($"DeepL: UpdateHandlerRegistration - Enabled: {moduleConfig?.Enabled}, IsConfigured: {moduleConfig?.IsConfigured()}, Handler.IsEnabled: {pipelineHandler.IsEnabled}");
        
        if (pipelineHandler.IsEnabled)
        {
            handlerRegistry.RegisterHandler(pipelineHandler, Name);
            Logger.Information($"DeepL handler registered/updated. Priority: {pipelineHandler.Priority}, APIKey set: {!string.IsNullOrWhiteSpace(moduleConfig?.ApiKey)}");
        }
        else
        {
            handlerRegistry.UnregisterHandler(pipelineHandler.Name);
            Logger.Information($"DeepL handler unregistered - Enabled: {moduleConfig?.Enabled}, HasApiKey: {!string.IsNullOrWhiteSpace(moduleConfig?.ApiKey)}");
        }
    }
    
    private void TriggerValidation(string apiKey)
    {
        // If the key is empty, return to the idle state
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            currentApiKeyStatus = ApiKeyStatus.Idle;
            lastValidatedApiKey = string.Empty;
            return;
        }
        
        // Skip if already validated
        if (apiKey == lastValidatedApiKey) return;
        
        // Set status to validating immediately for UI feedback
        currentApiKeyStatus = ApiKeyStatus.Validating;
        
        // Run validation completely off the main thread
        Task.Run(async () =>
        {
            // Add a small delay for debouncing
            await Task.Delay(500);
            await ValidateApiKeyAsync(apiKey);
        }).ContinueWith(task =>
        {
            if (task.IsFaulted)
            {
                Logger.Error($"Validation task failed: {task.Exception?.GetBaseException().Message}");
                framework?.RunOnFrameworkThread(() =>
                {
                    currentApiKeyStatus = ApiKeyStatus.Invalid;
                });
            }
        }, TaskScheduler.Default);
    }
    
    private async Task ValidateApiKeyAsync(string apiKey)
    {
        try
        {
            Logger.Debug($"DeepL: Starting API key validation");
            
            // Create temporary config and client for validation
            var tempConfig = new DeepLConfig 
            { 
                ApiKey = apiKey, 
                UsePro = moduleConfig?.UsePro ?? false,
                TimeoutMs = 5000  // Use shorter timeout for validation
            };
            
            // Use a cancellation token with timeout to prevent hanging
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var apiClient = new DeepLApiClient(tempConfig, Logger);
            
            var isValid = await apiClient.ValidateApiKeyAsync(cts.Token).ConfigureAwait(false);
            
            Logger.Debug($"DeepL: API key validation result: {isValid}");
            
            // Update the validation state on the main thread
            framework?.RunOnFrameworkThread(() =>
            {
                currentApiKeyStatus = isValid ? ApiKeyStatus.Valid : ApiKeyStatus.Invalid;
                lastValidatedApiKey = apiKey;
            });
        }
        catch (TaskCanceledException)
        {
            Logger.Warning("DeepL: API key validation timed out");
            framework?.RunOnFrameworkThread(() =>
            {
                currentApiKeyStatus = ApiKeyStatus.Invalid;
                lastValidatedApiKey = apiKey;
            });
        }
        catch (Exception ex)
        {
            Logger.Error($"DeepL: Error validating API key: {ex.Message}");
            framework?.RunOnFrameworkThread(() =>
            {
                currentApiKeyStatus = ApiKeyStatus.Invalid;
                lastValidatedApiKey = apiKey;
            });
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
            TriggerValidation(tempApiKey);
        }
        
        // Show validation status
        ImGui.SameLine();
        switch (currentApiKeyStatus)
        {
            case ApiKeyStatus.Validating:
                ImGui.TextDisabled("Validating...");
                break;
            case ApiKeyStatus.Valid:
                ImGui.PushFont(UiBuilder.IconFont);
                ImGui.TextColored(LayoutHelpers.Colors.Success, FontAwesomeIcon.CheckCircle.ToIconString());
                ImGui.PopFont();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("API key is valid");
                break;
            case ApiKeyStatus.Invalid:
                ImGui.PushFont(UiBuilder.IconFont);
                ImGui.TextColored(LayoutHelpers.Colors.Error, FontAwesomeIcon.TimesCircle.ToIconString());
                ImGui.PopFont();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("API key is invalid or has an issue");
                break;
            case ApiKeyStatus.Idle:
            default: // ApiKeyStatus.Idle
                ImGuiComponents.HelpMarker("Enter your DeepL API Key. Validation will start automatically.");
                break;
        }

        var isEnabled = moduleConfig.Enabled;
        if (ImGui.Checkbox("Enable DeepL Handler", ref isEnabled))
        {
            moduleConfig.Enabled = isEnabled;
            configChanged = true;
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker("Enables the DeepL provider in the translation pipeline.\nA valid API Key is required.");

        var usePro = moduleConfig.UsePro;
        if (ImGui.Checkbox("Use Pro API URL", ref usePro))
        {
            moduleConfig.UsePro = usePro;
            configChanged = true;
        }

        if (configChanged)
        {
            SaveConfiguration();
            EventBus.Publish(new DeepLConfigChanged());
        }
    }
}
