using System;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.IoC;
using Microsoft.Extensions.DependencyInjection;
using ModuleKit.Configuration;
using ModuleKit.Module;
using ModuleKit.Reactive;
using TLink.Modules.Chat;

namespace TLink;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService]
    public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    
    [PluginService]
    public static IPluginLog Log { get; private set; } = null!;

    [PluginService]
    public static ICommandManager CommandManager { get; private set; } = null!;
    
    [PluginService]
    public static IFramework Framework { get; private set; } = null!;
    
    [PluginService]
    public static IChatGui ChatGui { get; private set; } = null!;
    
    public static string Name => "TataruLink";
    
    private ModuleManager? moduleManager;
    private IServiceProvider? globalServices;
    private PluginConfiguration? configuration;
    private EventBus? eventBus;
    private bool disposed;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Plugin>();
        
        try
        {
            InitializeServices();
            LoadModules();
            
            // Hook into Dalamud's UI drawing
            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
            
            Log.Information("TataruLink initialized successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize TataruLink");
            throw;
        }
    }
    
    private void InitializeServices()
    {
        // Create global service collection
        var services = new ServiceCollection();
        
        // Register Dalamud services
        services.AddSingleton(PluginInterface);
        services.AddSingleton(Log);
        services.AddSingleton(CommandManager);
        services.AddSingleton(Framework);
        services.AddSingleton(ChatGui);
        
        // Register core services
        eventBus = new EventBus();
        services.AddSingleton(eventBus);
        
        // Initialize configuration
        configuration = new PluginConfiguration();
        configuration.Initialize(PluginInterface);
        services.AddSingleton(configuration);
        
        // Build global service provider
        globalServices = services.BuildServiceProvider();
        
        // Create module manager
        moduleManager = new ModuleManager(globalServices, Log);
    }
    
    private void LoadModules()
    {
        if (moduleManager == null) return;
        
        // Load modules in dependency order
        moduleManager.LoadModule<ChatModule>();
        // Future modules will be loaded here:
        // moduleManager.LoadModule<TranslationModule>();
        // moduleManager.LoadModule<MessageOutputModule>();
        
        Log.Information($"Loaded {moduleManager.LoadedModules.Count} modules");
    }
    
    private void DrawUI()
    {
        moduleManager?.DrawUI();
    }
    
    private void DrawConfigUI()
    {
        moduleManager?.DrawConfiguration();
    }

    public void Dispose()
    {
        Dispose(true);
    }
    
    private void Dispose(bool disposing)
    {
        if (disposed) return;
        
        if (disposing)
        {
            PluginInterface.UiBuilder.Draw -= DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;
            
            moduleManager?.Dispose();
            eventBus?.Dispose();
            configuration?.Save();
            
            if (globalServices is IDisposable disposableServices)
            {
                disposableServices.Dispose();
            }
            
            Log.Information("TataruLink plugin Disposed");
        }
        
        disposed = true;
    }
}
