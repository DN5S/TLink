using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using TLink.Core.Module;
using TLink.Core.MVU;
using TLink.Modules.Chat.Models;
using TLink.Modules.Translation.Configuration;
using TLink.Modules.Translation.Models;
using TLink.Modules.Translation.MVU;
using TLink.Modules.Translation.Services;
using TLink.Modules.Translation.UI;

namespace TLink.Modules.Translation;

[ModuleInfo("Translation", "1.0.0",
    Dependencies = ["Chat"],
    Description = "Translation hub for managing translation",
    Author = "DN5S",
    Priority = 10)]
public class TranslationModule : ModuleBase, ITranslationProviderRegistry
{
    private readonly Dictionary<string, ITranslationProvider> registeredProviders = new();
    private TranslationWindow? window;
    private TranslationViewModel? viewModel;
    private Store<TranslationState>? store;
    private TranslationConfig? moduleConfig;
    private ITranslationProvider? activeProvider;
    
    public override string Name => "Translation";
    public override string Version => "1.0.0";
    public override string[] Dependencies => ["Chat"];
    
    public override void RegisterServices(IServiceCollection services)
    {
        // Register the translation hub store
        services.AddSingleton<IStore<TranslationState>>(sp =>
        {
            store = new Store<TranslationState>(
                TranslationState.Initial,
                TranslationUpdate.Update
            );
            
            // Register effect handler for routing translations
            store.RegisterEffectHandler(new TranslateRoutingEffectHandler(
                () => activeProvider,
                sp.GetRequiredService<SeStringProcessor>(),
                EventBus,
                Logger
            ));
            
            return store;
        });
        
        // Register shared services
        services.AddSingleton<SeStringProcessor>();
        services.AddSingleton<TranslationViewModel>();
        
        // Register provider registry (for other modules to register providers)
        services.AddSingleton<ITranslationProviderRegistry>(_ => this);
    }
    
    protected override void LoadConfiguration()
    {
        moduleConfig = GetModuleConfig<TranslationConfig>();
    }
    
    public override void Initialize()
    {
        store = (Store<TranslationState>)Services.GetRequiredService<IStore<TranslationState>>();
        viewModel = Services.GetRequiredService<TranslationViewModel>();
        
        viewModel.Initialize(store, moduleConfig!, registeredProviders);
        
        window = new TranslationWindow(viewModel, moduleConfig!, () =>
        {
            SetModuleConfig(moduleConfig!);
            UpdateActiveProvider();
        });
        
        // Subscribe to chat messages from the Chat module
        Subscriptions.Add(
            EventBus.Listen<ChatMessageReceived>()
                .Subscribe(msg =>
                {
                    if (activeProvider != null)
                    {
                        store.Dispatch(new TranslateRequestAction(msg.Message));
                    }
                })
        );
        
        // Subscribe to translation results from providers
        Subscriptions.Add(
            EventBus.Listen<ProviderTranslationCompleted>()
                .Subscribe(result =>
                {
                    store.Dispatch(new TranslationCompletedAction(
                        result.Request,
                        result.Result
                    ));
                    
                    // Publish for other modules
                    EventBus.Publish(new MessageTranslatedEvent(
                        result.Request.Message,
                        result.Result
                    ));
                })
        );
        
        // Subscribe to translation failures from providers
        Subscriptions.Add(
            EventBus.Listen<ProviderTranslationFailed>()
                .Subscribe(failure =>
                {
                    store.Dispatch(new TranslationFailedAction(
                        failure.Request,
                        failure.Error
                    ));
                    
                    // Publish error for other modules
                    EventBus.Publish(new TranslationErrorEvent(
                        failure.Request.Message,
                        failure.Error
                    ));
                })
        );
        
        // Set an initial active provider
        UpdateActiveProvider();
        
        Logger.Information($"Translation hub initialized with {registeredProviders.Count} providers");
    }
    
    public void RegisterProvider(string name, ITranslationProvider provider)
    {
        if (registeredProviders.ContainsKey(name))
        {
            Logger.Warning($"Provider '{name}' is already registered, replacing");
        }
        
        registeredProviders[name] = provider;
        Logger.Information($"Translation provider '{name}' registered");
        
        // Update UI
        viewModel?.UpdateProviderList(registeredProviders);
        
        // If no active provider, set this as active
        if (activeProvider == null)
        {
            moduleConfig!.ActiveProvider = name;
            UpdateActiveProvider();
        }
    }
    
    public void UnregisterProvider(string name)
    {
        if (registeredProviders.Remove(name))
        {
            Logger.Information($"Translation provider '{name}' unregistered");
            
            // If this was the active provider, clear it
            if (moduleConfig?.ActiveProvider == name)
            {
                activeProvider = null;
                moduleConfig.ActiveProvider = string.Empty;
            }
            
            viewModel?.UpdateProviderList(registeredProviders);
        }
    }
    
    private void UpdateActiveProvider()
    {
        if (string.IsNullOrEmpty(moduleConfig?.ActiveProvider))
        {
            activeProvider = null;
            return;
        }
        
        if (registeredProviders.TryGetValue(moduleConfig.ActiveProvider, out var provider))
        {
            activeProvider = provider;
            Logger.Information($"Active translation provider set to '{moduleConfig.ActiveProvider}'");
            
            // Update state with provider info
            store?.Dispatch(new ProviderChangedAction(
                moduleConfig.ActiveProvider,
                provider.SupportsXmlTags
            ));
        }
        else
        {
            Logger.Warning($"Provider '{moduleConfig.ActiveProvider}' not found");
            activeProvider = null;
        }
    }
    
    public override void DrawUI()
    {
        window?.Draw();
    }
    
    public override void DrawConfiguration()
    {
        window?.DrawConfiguration();
    }
    
    public override void Dispose()
    {
        window?.Dispose();
        viewModel?.Dispose();
        store?.Dispose();
        
        // Notify providers about disposal
        foreach (var provider in registeredProviders.Values)
        {
            if (provider is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        
        registeredProviders.Clear();
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}

// Interface for provider registration
public interface ITranslationProviderRegistry
{
    void RegisterProvider(string name, ITranslationProvider provider);
    void UnregisterProvider(string name);
}
