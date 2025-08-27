using System;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SamplePlugin.Core.Module;
using SamplePlugin.Core.Reactive;
using SamplePlugin.Core.Configuration;
using Xunit;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace SamplePlugin.Tests.Core.Module;

public class ModuleManagerDependencyTests : IDisposable
{
    private readonly ServiceProvider serviceProvider;
    private readonly Mock<IPluginLog> mockLogger;
    private readonly ModuleManager moduleManager;
    private readonly PluginConfiguration configuration;
    
    public ModuleManagerDependencyTests()
    {
        mockLogger = new Mock<IPluginLog>();
        var mockPluginInterface = new Mock<IDalamudPluginInterface>();
        
        var services = new ServiceCollection();
        services.AddSingleton(mockLogger.Object);
        services.AddSingleton(mockPluginInterface.Object);
        services.AddSingleton<EventBus>();
        
        // Add PluginConfiguration
        configuration = new PluginConfiguration();
        configuration.Initialize(mockPluginInterface.Object);
        services.AddSingleton(configuration);
        
        serviceProvider = services.BuildServiceProvider();
        
        moduleManager = new ModuleManager(serviceProvider, mockLogger.Object);
        
        // Register test modules in the registry
        RegisterTestModules();
    }
    
    private void RegisterTestModules()
    {
        var registry = moduleManager.Registry;
        
        // Register a base module with no dependencies
        registry.RegisterModule("BaseModule", typeof(TestBaseModule), 
            new ModuleInfoAttribute("BaseModule", "1.0.0"));
        
        // Register module that depends on BaseModule
        registry.RegisterModule("DependentModule", typeof(TestDependentModule),
            new ModuleInfoAttribute("DependentModule", "1.0.0") { Dependencies = ["BaseModule"] });
        
        // Register module with chain dependency (depends on DependentModule, which depends on BaseModule)
        registry.RegisterModule("ChainedModule", typeof(TestChainedModule),
            new ModuleInfoAttribute("ChainedModule", "1.0.0") { Dependencies = ["DependentModule"] });
        
        // Register module with multiple dependencies
        registry.RegisterModule("MultiDependentModule", typeof(TestMultiDependentModule),
            new ModuleInfoAttribute("MultiDependentModule", "1.0.0") 
            { 
                Dependencies = ["BaseModule", "DependentModule"] 
            });
        
        // Register independent module
        registry.RegisterModule("IndependentModule", typeof(TestIndependentModule),
            new ModuleInfoAttribute("IndependentModule", "1.0.0"));
    }
    
    #region Dependency Checking Tests
    
    [Fact]
    public void GetDependentModules_WithNoDependents_ShouldReturnEmpty()
    {
        // Arrange
        moduleManager.LoadModule(new TestIndependentModule());
        
        // Act
        var dependents = moduleManager.GetDependentModules("IndependentModule");
        
        // Assert
        dependents.Should().BeEmpty();
    }
    
    [Fact]
    public void GetDependentModules_WithDirectDependents_ShouldReturnCorrectList()
    {
        // Arrange
        moduleManager.LoadModule(new TestBaseModule());
        moduleManager.LoadModule(new TestDependentModule());
        
        // Act
        var dependents = moduleManager.GetDependentModules("BaseModule");
        
        // Assert
        // MultiDependentModule is also registered and depends on BaseModule
        dependents.Should().Contain("DependentModule");
        dependents.Should().Contain("MultiDependentModule");
    }
    
    [Fact]
    public void GetAllDependentModules_WithTransitiveDependencies_ShouldReturnAllDependents()
    {
        // Arrange
        moduleManager.LoadModule(new TestBaseModule());
        moduleManager.LoadModule(new TestDependentModule());
        moduleManager.LoadModule(new TestChainedModule());
        
        // Act
        var allDependents = moduleManager.GetAllDependentModules("BaseModule");
        
        // Assert
        // Includes all modules that depend on BaseModule directly or indirectly
        allDependents.Should().Contain("DependentModule");
        allDependents.Should().Contain("ChainedModule");
        allDependents.Should().Contain("MultiDependentModule"); // Also registered
    }
    
    [Fact]
    public void CanDisableModule_WithNoDependents_ShouldReturnTrue()
    {
        // Arrange
        moduleManager.LoadModule(new TestIndependentModule());
        
        // Act
        var (canDisable, dependents) = moduleManager.CanDisableModule("IndependentModule");
        
        // Assert
        canDisable.Should().BeTrue();
        dependents.Should().BeEmpty();
    }
    
    [Fact]
    public void CanDisableModule_WithDependents_ShouldReturnFalseAndListDependents()
    {
        // Arrange
        moduleManager.LoadModule(new TestBaseModule());
        moduleManager.LoadModule(new TestDependentModule());
        moduleManager.LoadModule(new TestChainedModule());
        
        // Act
        var (canDisable, dependents) = moduleManager.CanDisableModule("BaseModule");
        
        // Assert
        canDisable.Should().BeFalse();
        // All registered modules that depend on BaseModule
        dependents.Should().Contain("DependentModule");
        dependents.Should().Contain("ChainedModule");
        dependents.Should().Contain("MultiDependentModule");
    }
    
    [Fact]
    public void AreDependenciesSatisfied_WithAllDependenciesEnabled_ShouldReturnTrue()
    {
        // Arrange
        configuration.SetModuleConfig("BaseModule", new ModuleConfiguration 
        { 
            ModuleName = "BaseModule", 
            IsEnabled = true 
        });
        
        // Act
        var satisfied = moduleManager.AreDependenciesSatisfied("DependentModule", configuration);
        
        // Assert
        satisfied.Should().BeTrue();
    }
    
    [Fact]
    public void AreDependenciesSatisfied_WithDisabledDependencies_ShouldReturnFalse()
    {
        // Arrange
        configuration.SetModuleConfig("BaseModule", new ModuleConfiguration 
        { 
            ModuleName = "BaseModule", 
            IsEnabled = false 
        });
        
        // Act
        var satisfied = moduleManager.AreDependenciesSatisfied("DependentModule", configuration);
        
        // Assert
        satisfied.Should().BeFalse();
    }
    
    [Fact]
    public void AreDependenciesSatisfied_ForNonExistentModule_ShouldReturnFalse()
    {
        // Act
        var satisfied = moduleManager.AreDependenciesSatisfied("NonExistentModule", configuration);
        
        // Assert
        satisfied.Should().BeFalse();
    }
    
    #endregion
    
    #region Dynamic Module Loading/Unloading Tests
    
    [Fact]
    public void ApplyConfigurationChanges_WhenModuleDisabled_ShouldUnloadModule()
    {
        // Arrange
        moduleManager.LoadModule(new TestBaseModule());
        moduleManager.LoadModule(new TestIndependentModule());
        
        configuration.SetModuleConfig("IndependentModule", new ModuleConfiguration 
        { 
            ModuleName = "IndependentModule", 
            IsEnabled = false 
        });
        
        // Act
        moduleManager.ApplyConfigurationChanges(configuration);
        
        // Assert
        moduleManager.LoadedModules.Should().NotContain(m => m.Name == "IndependentModule");
        moduleManager.LoadedModules.Should().Contain(m => m.Name == "BaseModule");
    }
    
    [Fact]
    public void ApplyConfigurationChanges_WhenModuleEnabled_ShouldLoadModule()
    {
        // Arrange - Start with no modules loaded
        configuration.SetModuleConfig("IndependentModule", new ModuleConfiguration 
        { 
            ModuleName = "IndependentModule", 
            IsEnabled = true 
        });
        
        // Act
        moduleManager.ApplyConfigurationChanges(configuration);
        
        // Assert
        moduleManager.LoadedModules.Should().ContainSingle(m => m.Name == "IndependentModule");
    }
    
    [Fact]
    public void ApplyConfigurationChanges_WithDependencyConflicts_ShouldHandleCorrectly()
    {
        // Arrange - Enable DependentModule but disable BaseModule
        configuration.SetModuleConfig("BaseModule", new ModuleConfiguration 
        { 
            ModuleName = "BaseModule", 
            IsEnabled = false 
        });
        configuration.SetModuleConfig("DependentModule", new ModuleConfiguration 
        { 
            ModuleName = "DependentModule", 
            IsEnabled = true 
        });
        
        // Act
        moduleManager.ApplyConfigurationChanges(configuration);
        
        // Assert - DependentModule should not be loaded due to a missing dependency
        moduleManager.LoadedModules.Should().NotContain(m => m.Name == "DependentModule");
        mockLogger.Verify(x => x.Warning(It.Is<string>(s => 
            s.Contains("Cannot load module DependentModule because dependency BaseModule is not enabled"))), 
            Times.Once);
    }
    
    [Fact]
    public void ApplyConfigurationChanges_ShouldLoadModulesInDependencyOrder()
    {
        // Arrange
        configuration.SetModuleConfig("BaseModule", new ModuleConfiguration 
        { 
            ModuleName = "BaseModule", 
            IsEnabled = true 
        });
        configuration.SetModuleConfig("DependentModule", new ModuleConfiguration 
        { 
            ModuleName = "DependentModule", 
            IsEnabled = true 
        });
        configuration.SetModuleConfig("ChainedModule", new ModuleConfiguration 
        { 
            ModuleName = "ChainedModule", 
            IsEnabled = true 
        });
        
        // Act
        moduleManager.ApplyConfigurationChanges(configuration);
        
        // Assert
        var loadedModuleNames = moduleManager.LoadedModules.Select(m => m.Name).ToList();
        loadedModuleNames.Should().ContainInOrder("BaseModule", "DependentModule", "ChainedModule");
    }
    
    [Fact]
    public void ApplyConfigurationChanges_WhenDisablingModuleWithDependents_ShouldUnloadAll()
    {
        // Arrange - Load all modules first
        moduleManager.LoadModule(new TestBaseModule());
        moduleManager.LoadModule(new TestDependentModule());
        moduleManager.LoadModule(new TestChainedModule());
        
        // Disable base module (which should cascade to dependents)
        configuration.SetModuleConfig("BaseModule", new ModuleConfiguration 
        { 
            ModuleName = "BaseModule", 
            IsEnabled = false 
        });
        
        // Act
        moduleManager.ApplyConfigurationChanges(configuration);
        
        // Assert - BaseModule and its dependents should be unloaded
        moduleManager.LoadedModules.Should().NotContain(m => m.Name == "BaseModule");
        moduleManager.LoadedModules.Should().NotContain(m => m.Name == "DependentModule");
        moduleManager.LoadedModules.Should().NotContain(m => m.Name == "ChainedModule");
        // IndependentModule was loaded by ApplyConfigurationChanges if it's enabled by default
    }
    
    [Fact]
    public void ApplyConfigurationChanges_WithMultipleDependencies_ShouldLoadCorrectly()
    {
        // Arrange - Explicitly disable all modules first
        configuration.SetModuleConfig("BaseModule", new ModuleConfiguration 
        { 
            ModuleName = "BaseModule", 
            IsEnabled = true 
        });
        configuration.SetModuleConfig("DependentModule", new ModuleConfiguration 
        { 
            ModuleName = "DependentModule", 
            IsEnabled = true 
        });
        configuration.SetModuleConfig("MultiDependentModule", new ModuleConfiguration 
        { 
            ModuleName = "MultiDependentModule", 
            IsEnabled = true 
        });
        configuration.SetModuleConfig("ChainedModule", new ModuleConfiguration 
        { 
            ModuleName = "ChainedModule", 
            IsEnabled = false 
        });
        configuration.SetModuleConfig("IndependentModule", new ModuleConfiguration 
        { 
            ModuleName = "IndependentModule", 
            IsEnabled = false 
        });
        
        // Act
        moduleManager.ApplyConfigurationChanges(configuration);
        
        // Assert
        moduleManager.LoadedModules.Should().HaveCount(3);
        moduleManager.LoadedModules.Should().Contain(m => m.Name == "BaseModule");
        moduleManager.LoadedModules.Should().Contain(m => m.Name == "DependentModule");
        moduleManager.LoadedModules.Should().Contain(m => m.Name == "MultiDependentModule");
    }
    
    #endregion
    
    #region Integration Tests for Real Modules
    
    [Fact]
    public void DisablingChat_ShouldAlsoDisableChatAnalyzer_IntegrationTest()
    {
        // Arrange - Discover real modules
        moduleManager.Registry.DiscoverModules();
        
        // Enable both modules initially
        configuration.SetModuleConfig("Chat", new ModuleConfiguration 
        { 
            ModuleName = "Chat", 
            IsEnabled = true 
        });
        configuration.SetModuleConfig("ChatAnalyzer", new ModuleConfiguration 
        { 
            ModuleName = "ChatAnalyzer", 
            IsEnabled = true 
        });
        
        // Load modules
        moduleManager.ApplyConfigurationChanges(configuration);
        var initialModules = moduleManager.LoadedModules.Select(m => m.Name).ToList();
        
        // Disable Chat module
        configuration.SetModuleConfig("Chat", new ModuleConfiguration 
        { 
            ModuleName = "Chat", 
            IsEnabled = false 
        });
        
        // Act
        moduleManager.ApplyConfigurationChanges(configuration);
        
        // Assert
        if (initialModules.Contains("Chat") && initialModules.Contains("ChatAnalyzer"))
        {
            // If both modules were loaded, neither should be loaded after disabling Chat
            moduleManager.LoadedModules.Should().NotContain(m => m.Name == "Chat");
            moduleManager.LoadedModules.Should().NotContain(m => m.Name == "ChatAnalyzer");
        }
    }
    
    [Fact]
    public void EnablingChatAnalyzer_WithDisabledChat_ShouldNotLoad()
    {
        // Arrange - Discover real modules
        moduleManager.Registry.DiscoverModules();
        
        // Disable Chat but enable ChatAnalyzer
        configuration.SetModuleConfig("Chat", new ModuleConfiguration 
        { 
            ModuleName = "Chat", 
            IsEnabled = false 
        });
        configuration.SetModuleConfig("ChatAnalyzer", new ModuleConfiguration 
        { 
            ModuleName = "ChatAnalyzer", 
            IsEnabled = true 
        });
        
        // Act
        moduleManager.ApplyConfigurationChanges(configuration);
        
        // Assert
        moduleManager.LoadedModules.Should().NotContain(m => m.Name == "ChatAnalyzer");
        
        // Verify warning was logged if the module exists
        if (moduleManager.Registry.ModuleInfos.ContainsKey("ChatAnalyzer"))
        {
            mockLogger.Verify(x => x.Warning(It.Is<string>(s => 
                s.Contains("Cannot load module ChatAnalyzer because dependency Chat is not enabled"))), 
                Times.AtLeastOnce);
        }
    }
    
    #endregion
    
    public void Dispose()
    {
        moduleManager?.Dispose();
        serviceProvider?.Dispose();
    }
    
    #region Test Helper Classes
    
    private class TestBaseModule : ModuleBase
    {
        public override string Name => "BaseModule";
        public override string Version => "1.0.0";
        public override string[] Dependencies => [];
        
        public override void RegisterServices(IServiceCollection services) { }
        protected override void LoadConfiguration() { }
        public override void Initialize() { }
        public override void DrawUI() { }
        public override void DrawConfiguration() { }
    }
    
    private class TestDependentModule : ModuleBase
    {
        public override string Name => "DependentModule";
        public override string Version => "1.0.0";
        public override string[] Dependencies => ["BaseModule"];
        
        public override void RegisterServices(IServiceCollection services) { }
        protected override void LoadConfiguration() { }
        public override void Initialize() { }
        public override void DrawUI() { }
        public override void DrawConfiguration() { }
    }
    
    private class TestChainedModule : ModuleBase
    {
        public override string Name => "ChainedModule";
        public override string Version => "1.0.0";
        public override string[] Dependencies => ["DependentModule"];
        
        public override void RegisterServices(IServiceCollection services) { }
        protected override void LoadConfiguration() { }
        public override void Initialize() { }
        public override void DrawUI() { }
        public override void DrawConfiguration() { }
    }
    
    private class TestMultiDependentModule : ModuleBase
    {
        public override string Name => "MultiDependentModule";
        public override string Version => "1.0.0";
        public override string[] Dependencies => ["BaseModule", "DependentModule"];
        
        public override void RegisterServices(IServiceCollection services) { }
        protected override void LoadConfiguration() { }
        public override void Initialize() { }
        public override void DrawUI() { }
        public override void DrawConfiguration() { }
    }
    
    private class TestIndependentModule : ModuleBase
    {
        public override string Name => "IndependentModule";
        public override string Version => "1.0.0";
        public override string[] Dependencies => [];
        
        public override void RegisterServices(IServiceCollection services) { }
        protected override void LoadConfiguration() { }
        public override void Initialize() { }
        public override void DrawUI() { }
        public override void DrawConfiguration() { }
    }
    
    #endregion
}
