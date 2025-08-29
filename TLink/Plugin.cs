using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using TLink.Core.Configuration;
using TLink.Core.Module;
using TLink.Core.Reactive;
using TLink.Core.UI;
using TLink.Utils;

namespace TLink;

public sealed class Plugin : IDalamudPlugin
{
    private readonly ServiceProvider serviceProvider;
    private readonly WindowSystem windowSystem;
    private readonly ModuleManager moduleManager;
    private readonly PluginConfiguration configuration;
    private readonly IJsonTypeInfoResolver jsonTypeResolver;
    private readonly MainWindow mainWindow;
    private readonly ConfigurationWindow configWindow;
    
    public Plugin(
        IDalamudPluginInterface pluginInterface,
        IFramework framework,
        ICommandManager commandManager,
        IChatGui chatGui,
        IPluginLog pluginLog)
    {
        // Create a window system
        windowSystem = new WindowSystem("TataruLink");
        
        // Discover all module configuration types dynamically
        var assembly = Assembly.GetExecutingAssembly();
        var configTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.IsSubclassOf(typeof(ModuleConfiguration)))
            .ToList();
        configTypes.Add(typeof(ModuleConfiguration)); // Add base class
        
        // Create a JSON type resolver with all discovered types
        jsonTypeResolver = JsonTypeInfoResolver.Combine(
            new DefaultJsonTypeInfoResolver()
            {
                Modifiers = { CreatePolymorphicModifier(configTypes) }
            }
        );
        
        // Initialize configuration with type resolver
        configuration = new PluginConfiguration();
        configuration.Initialize(pluginInterface, jsonTypeResolver);
        
        // Setup DI container
        var services = new ServiceCollection();
        
        // Register Dalamud services
        services.AddSingleton(pluginInterface);
        services.AddSingleton(framework);
        services.AddSingleton(commandManager);
        services.AddSingleton(chatGui);
        services.AddSingleton(pluginLog);
        services.AddSingleton(windowSystem);
        
        // Register core services
        services.AddSingleton<EventBus>();
        services.AddSingleton<ModuleManager>();
        services.AddSingleton(configuration);
        
        // Plugin specific core services
        services.AddSingleton<SeStringProcessor>();
        
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
        commandManager.AddHandler("/tlink", new Dalamud.Game.Command.CommandInfo((_, _) => 
        {
            OpenMainUI();
        })
        {
            HelpMessage = "Open TataruLink window"
        });
        
        pluginLog.Information("TataruLink loaded successfully!");
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
        commandManager.RemoveHandler("/tlink");
        
        // Save configuration with type resolver before disposing
        configuration.Save();
        
        // Dispose windows
        windowSystem.RemoveAllWindows();
        mainWindow.Dispose();
        configWindow.Dispose();
        
        moduleManager.Dispose();
        serviceProvider.Dispose();
    }
    
    private static Action<JsonTypeInfo> CreatePolymorphicModifier(List<Type> configTypes)
    {
        return typeInfo =>
        {
            if (typeInfo.Type == typeof(ModuleConfiguration))
            {
                typeInfo.PolymorphismOptions = new JsonPolymorphismOptions
                {
                    TypeDiscriminatorPropertyName = "$type",
                    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization
                };
                
                foreach (var type in configTypes)
                {
                    var discriminator = type.Name
                        .Replace("Configuration", "")
                        .Replace("Config", "");
                    if (string.IsNullOrEmpty(discriminator) || discriminator == "Module")
                        discriminator = "Base";
                    
                    typeInfo.PolymorphismOptions.DerivedTypes.Add(
                        new JsonDerivedType(type, discriminator)
                    );
                }
            }
        };
    }
}
