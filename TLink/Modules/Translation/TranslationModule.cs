using System;
using System.Collections.Generic;
using System.Linq;
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

/// <summary>
/// Translation Orchestrator Module - Pure pipeline orchestration.
/// This module knows nothing about how translation works, it only manages the pipeline execution.
/// </summary>
[ModuleInfo("Translation", "2.0.0",
    Dependencies = ["Chat"],
    Description = "Translation pipeline orchestrator",
    Author = "DN5S",
    Priority = 10)]
public class TranslationModule : ModuleBase, IPipelineHandlerRegistry
{
    private readonly List<ITranslationPipelineHandler> registeredHandlers = new();
    private TranslationWindow? window;
    private TranslationViewModel? viewModel;
    private Store<TranslationState>? store;
    private TranslationConfig? moduleConfig;
    
    public override string Name => "Translation";
    public override string Version => "2.0.0";
    public override string[] Dependencies => ["Chat"];
    
    public override void RegisterServices(IServiceCollection services)
    {
        // Register the orchestrator store
        services.AddSingleton<IStore<TranslationState>>(_ =>
        {
            store = new Store<TranslationState>(
                TranslationState.Initial,
                TranslationUpdate.Update
            );
            
            // Register pipeline execution handler
            store.RegisterEffectHandler(new PipelineExecutionEffectHandler(
                GetHandlers,
                store,
                EventBus,
                Logger
            ));
            
            // Register event publishing handlers
            store.RegisterEffectHandler(new PublishHandlerRegisteredEffectHandler(EventBus));
            store.RegisterEffectHandler(new PublishHandlerUnregisteredEffectHandler(EventBus));
            store.RegisterEffectHandler(new PublishPipelineStartedEffectHandler(EventBus));
            store.RegisterEffectHandler(new PublishPipelineCompletedEffectHandler(EventBus));
            store.RegisterEffectHandler(new PublishTranslatedMessageEffectHandler(EventBus));
            store.RegisterEffectHandler(new PublishTranslationErrorEffectHandler(EventBus));
            
            return store;
        });
        
        // Register shared services that handlers might need
        services.AddSingleton<SeStringProcessor>();
        services.AddSingleton<TranslationViewModel>();
        
        // Register this module as the pipeline handler registry
        services.AddSingleton<IPipelineHandlerRegistry>(_ => this);
    }
    
    protected override void LoadConfiguration()
    {
        moduleConfig = GetModuleConfig<TranslationConfig>();
    }
    
    public override void Initialize()
    {
        store = (Store<TranslationState>)Services.GetRequiredService<IStore<TranslationState>>();
        viewModel = Services.GetRequiredService<TranslationViewModel>();
        
        // Initialize a view model with the store
        viewModel.Initialize(store);
        
        window = new TranslationWindow(viewModel, moduleConfig!, () =>
        {
            SetModuleConfig(moduleConfig!);
        });
        
        // Subscribe to chat messages and execute a pipeline for each
        Subscriptions.Add(
            EventBus.Listen<ChatMessageReceived>()
                .Subscribe(msg =>
                {
                    // Execute the pipeline for each message
                    store.Dispatch(new ExecutePipelineAction(
                        msg.Message,
                        moduleConfig?.SourceLanguage ?? "auto",
                        moduleConfig?.TargetLanguage ?? "en"
                    ));
                })
        );
        
        Logger.Information("Translation orchestrator initialized");
    }
    
    // --- IPipelineHandlerRegistry Implementation ---
    
    public void RegisterHandler(ITranslationPipelineHandler handler)
    {
        if (registeredHandlers.Any(h => h.Name == handler.Name))
        {
            Logger.Warning($"Handler '{handler.Name}' is already registered, replacing");
            registeredHandlers.RemoveAll(h => h.Name == handler.Name);
        }
        
        registeredHandlers.Add(handler);
        registeredHandlers.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        
        // Notify store about registration
        store?.Dispatch(new RegisterHandlerAction(handler, handler.GetType().Module.Name));
        
        Logger.Information($"Pipeline handler '{handler.Name}' registered with priority {handler.Priority}");
    }
    
    public bool UnregisterHandler(string handlerName)
    {
        var handler = registeredHandlers.FirstOrDefault(h => h.Name == handlerName);
        if (handler != null)
        {
            registeredHandlers.Remove(handler);
            
            // Notify store about unregistration
            store?.Dispatch(new UnregisterHandlerAction(handlerName));
            
            Logger.Information($"Pipeline handler '{handlerName}' unregistered");
            return true;
        }
        
        return false;
    }
    
    public IReadOnlyList<ITranslationPipelineHandler> GetHandlers()
    {
        return registeredHandlers.AsReadOnly();
    }
    
    public ITranslationPipelineHandler? GetHandler(string handlerName)
    {
        return registeredHandlers.FirstOrDefault(h => h.Name == handlerName);
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
        // Dispose UI components
        window?.Dispose();
        viewModel?.Dispose();
        
        // Dispose store
        store?.Dispose();
        
        // Clear handlers
        registeredHandlers.Clear();
        
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
