using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Windowing;
using Microsoft.Extensions.DependencyInjection;
using SamplePlugin.Core.Module;
using SamplePlugin.Core.Reactive;
using SamplePlugin.Core.Configuration;
using SamplePlugin.Core.UI;

namespace SamplePlugin;

public sealed class Plugin : IDalamudPlugin
{
    private readonly ServiceProvider serviceProvider;
    private readonly WindowSystem windowSystem;
    private readonly ModuleManager moduleManager;
    private readonly PluginConfiguration configuration;
    private readonly MainWindow mainWindow;
    private readonly ConfigurationWindow configWindow;
    
    public Plugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IChatGui chatGui,
        IPluginLog pluginLog)
    {
        // Create a window system
        windowSystem = new WindowSystem("SamplePlugin");
        
        // Initialize configuration
        configuration = new PluginConfiguration();
        configuration.Initialize(pluginInterface);
        
        // Setup DI container
        var services = new ServiceCollection();
        
        // Register Dalamud services
        services.AddSingleton(pluginInterface);
        services.AddSingleton(commandManager);
        services.AddSingleton(chatGui);
        services.AddSingleton(pluginLog);
        services.AddSingleton(windowSystem);
        
        // Register core services
        services.AddSingleton<EventBus>();
        services.AddSingleton<ModuleManager>();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(configuration);
        
        // Build service provider
        serviceProvider = services.BuildServiceProvider();
        
        // Initialize module manager
        moduleManager = serviceProvider.GetRequiredService<ModuleManager>();
        
        // Create main windows
        configWindow = new ConfigurationWindow(moduleManager, configuration);
        mainWindow = new MainWindow(moduleManager, configuration, () => configWindow.IsOpen = true);
        
        // Register windows
        windowSystem.AddWindow(mainWindow);
        windowSystem.AddWindow(configWindow);
        
        // Load modules using a discovery system
        moduleManager.LoadAllRegisteredModules(configuration);
        
        // Register UI events
        pluginInterface.UiBuilder.Draw += DrawUI;
        pluginInterface.UiBuilder.OpenMainUi += OpenMainUI;
        pluginInterface.UiBuilder.OpenConfigUi += OpenConfigUI;
        
        // Register commands
        commandManager.AddHandler("/sampleplugin", new Dalamud.Game.Command.CommandInfo((_, _) => 
        {
            OpenMainUI();
        })
        {
            HelpMessage = "Open SamplePlugin window"
        });
        
        pluginLog.Information("SamplePlugin loaded successfully!");
    }
    
    
    private void DrawUI()
    {
        windowSystem.Draw();
        moduleManager.DrawUI();
    }
    
    private void OpenMainUI()
    {
        mainWindow.IsOpen = true;
    }
    
    private void OpenConfigUI()
    {
        configWindow.IsOpen = true;
    }
    
    public void Dispose()
    {
        var pluginInterface = serviceProvider.GetRequiredService<IDalamudPluginInterface>();
        pluginInterface.UiBuilder.Draw -= DrawUI;
        pluginInterface.UiBuilder.OpenMainUi -= OpenMainUI;
        pluginInterface.UiBuilder.OpenConfigUi -= OpenConfigUI;
        
        var commandManager = serviceProvider.GetRequiredService<ICommandManager>();
        commandManager.RemoveHandler("/sampleplugin");
        
        // Save configuration before disposing
        configuration.Save();
        
        // Dispose windows
        windowSystem.RemoveAllWindows();
        mainWindow.Dispose();
        configWindow.Dispose();
        
        moduleManager.Dispose();
        serviceProvider.Dispose();
    }
}
