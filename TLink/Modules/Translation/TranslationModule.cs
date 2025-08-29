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
    private readonly List<ITranslationPipelineHandler> registeredHandlers = [];
    private TranslationWindow? window;
    private TranslationViewModel? viewModel;
    private Store<TranslationState>? store;
    private TranslationConfig? moduleConfig;
    
    public override string Name => "Translation";
    public override string Version => "1.0.1";
    public override string[] Dependencies => ["Chat"];
    
    protected override void LoadConfiguration()
    {
        moduleConfig = GetModuleConfig<TranslationConfig>();
    }
    
    public override void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<IStore<TranslationState>>(_ => new Store<TranslationState>(
            TranslationState.Initial,
            TranslationUpdate.Update
        ));
        
        services.AddSingleton<TranslationViewModel>();
    }
    
    public override void RegisterSharedServices(IServiceCollection services)
    {
        services.AddSingleton<IPipelineHandlerRegistry>(_ => this);
    }
    
    public override void Initialize()
    {
        store = (Store<TranslationState>)Services.GetRequiredService<IStore<TranslationState>>();
        viewModel = Services.GetRequiredService<TranslationViewModel>();
        
        store.RegisterEffectHandler(new PipelineExecutionEffectHandler(GetHandlers, store, EventBus, Logger));
        store.RegisterEffectHandler(new PublishHandlerRegisteredEffectHandler(EventBus));
        store.RegisterEffectHandler(new PublishHandlerUnregisteredEffectHandler(EventBus));
        store.RegisterEffectHandler(new PublishPipelineStartedEffectHandler(EventBus));
        store.RegisterEffectHandler(new PublishPipelineCompletedEffectHandler(EventBus));
        store.RegisterEffectHandler(new PublishTranslatedMessageEffectHandler(EventBus));
        store.RegisterEffectHandler(new PublishTranslationErrorEffectHandler(EventBus));
        
        viewModel.Initialize(store);
        
        window = new TranslationWindow(viewModel, moduleConfig!, () =>
        {
            SetModuleConfig(moduleConfig!);
        });
        
        // Subscribe to translatable messages and execute a pipeline for each
        Subscriptions.Add(
            EventBus.Listen<TranslatableMessageReceived>()
                .Subscribe(msg =>
                {
                    // Execute the pipeline asynchronously to avoid blocking the main thread
                    // a Fire-and-forget pattern prevents main thread freezing during network operations
                    _ = store.DispatchAsync(new ExecutePipelineAction(
                        msg.Message,
                        moduleConfig?.SourceLanguage ?? "auto",
                        moduleConfig?.TargetLanguage ?? "en"
                    ));
                })
        );
        
        Logger.Information("Translation orchestrator initialized");
    }
    
    public void RegisterHandler(ITranslationPipelineHandler handler, string moduleName)
    {
        if (registeredHandlers.Any(h => h.Name == handler.Name))
        {
            Logger.Warning($"Handler '{handler.Name}' is already registered, replacing");
            registeredHandlers.RemoveAll(h => h.Name == handler.Name);
        }
        
        registeredHandlers.Add(handler);
        registeredHandlers.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        
        // Notify the store about registration with an explicit module name
        store?.Dispatch(new RegisterHandlerAction(handler, moduleName));
        
        Logger.Information($"Pipeline handler '{handler.Name}' from module '{moduleName}' registered with priority {handler.Priority}");
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
