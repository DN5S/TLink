using System;
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

/// <summary>
/// Tests for the configuration-aware dependency checking
/// </summary>
public class ModuleManagerEnabledDependencyTests : IDisposable
{
    private readonly ServiceProvider serviceProvider;
    private readonly Mock<IPluginLog> mockLogger;
    private readonly ModuleManager moduleManager;
    private readonly PluginConfiguration configuration;
    
    public ModuleManagerEnabledDependencyTests()
    {
        mockLogger = new Mock<IPluginLog>();
        var mockPluginInterface = new Mock<IDalamudPluginInterface>();
        
        var services = new ServiceCollection();
        services.AddSingleton(mockLogger.Object);
        services.AddSingleton(mockPluginInterface.Object);
        services.AddSingleton<EventBus>();
        
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
        
        registry.RegisterModule("BaseModule", typeof(TestBaseModule), 
            new ModuleInfoAttribute("BaseModule", "1.0.0"));
        
        registry.RegisterModule("DependentModule1", typeof(TestDependentModule),
            new ModuleInfoAttribute("DependentModule1", "1.0.0") { Dependencies = ["BaseModule"] });
        
        registry.RegisterModule("DependentModule2", typeof(TestDependentModule),
            new ModuleInfoAttribute("DependentModule2", "1.0.0") { Dependencies = ["BaseModule"] });
    }
    
    [Fact]
    public void CanDisableModule_WithOnlyDisabledDependents_ShouldReturnTrue()
    {
        // Arrange - Set up modules where dependents are disabled
        configuration.SetModuleConfig("BaseModule", new ModuleConfiguration 
        { 
            ModuleName = "BaseModule", 
            IsEnabled = true 
        });
        configuration.SetModuleConfig("DependentModule1", new ModuleConfiguration 
        { 
            ModuleName = "DependentModule1", 
            IsEnabled = false  // Disabled
        });
        configuration.SetModuleConfig("DependentModule2", new ModuleConfiguration 
        { 
            ModuleName = "DependentModule2", 
            IsEnabled = false  // Disabled
        });
        
        // Act
        var (canDisable, dependents) = moduleManager.CanDisableModule("BaseModule", configuration);
        
        // Assert
        canDisable.Should().BeTrue("all dependents are already disabled");
        dependents.Should().BeEmpty("no enabled dependents should be returned");
    }
    
    [Fact]
    public void CanDisableModule_WithEnabledDependents_ShouldReturnFalseAndListOnlyEnabled()
    {
        // Arrange - One enabled, one disabled dependent
        configuration.SetModuleConfig("BaseModule", new ModuleConfiguration 
        { 
            ModuleName = "BaseModule", 
            IsEnabled = true 
        });
        configuration.SetModuleConfig("DependentModule1", new ModuleConfiguration 
        { 
            ModuleName = "DependentModule1", 
            IsEnabled = true  // Enabled
        });
        configuration.SetModuleConfig("DependentModule2", new ModuleConfiguration 
        { 
            ModuleName = "DependentModule2", 
            IsEnabled = false  // Disabled
        });
        
        // Act
        var (canDisable, dependents) = moduleManager.CanDisableModule("BaseModule", configuration);
        
        // Assert
        canDisable.Should().BeFalse("there is an enabled dependent");
        dependents.Should().ContainSingle();
        dependents.Should().Contain("DependentModule1", "only enabled dependent should be returned");
        dependents.Should().NotContain("DependentModule2", "disabled dependents should not be returned");
    }
    
    [Fact]
    public void CanDisableModule_WithAllEnabledDependents_ShouldReturnAllDependents()
    {
        // Arrange - All dependents enabled
        configuration.SetModuleConfig("BaseModule", new ModuleConfiguration 
        { 
            ModuleName = "BaseModule", 
            IsEnabled = true 
        });
        configuration.SetModuleConfig("DependentModule1", new ModuleConfiguration 
        { 
            ModuleName = "DependentModule1", 
            IsEnabled = true  // Enabled
        });
        configuration.SetModuleConfig("DependentModule2", new ModuleConfiguration 
        { 
            ModuleName = "DependentModule2", 
            IsEnabled = true  // Enabled
        });
        
        // Act
        var (canDisable, dependents) = moduleManager.CanDisableModule("BaseModule", configuration);
        
        // Assert
        canDisable.Should().BeFalse("there are enabled dependents");
        dependents.Should().HaveCount(2);
        dependents.Should().Contain("DependentModule1");
        dependents.Should().Contain("DependentModule2");
    }
    
    [Fact]
    public void CanDisableModule_OverloadWithoutConfig_ShouldReturnAllDependents()
    {
        // Arrange - Register modules (enabled status doesn't matter for this overload)
        configuration.SetModuleConfig("DependentModule1", new ModuleConfiguration 
        { 
            ModuleName = "DependentModule1", 
            IsEnabled = false  // Disabled
        });
        configuration.SetModuleConfig("DependentModule2", new ModuleConfiguration 
        { 
            ModuleName = "DependentModule2", 
            IsEnabled = false  // Disabled
        });
        
        // Act - Use the overload WITHOUT configuration
        var (canDisable, dependents) = moduleManager.CanDisableModule("BaseModule");
        
        // Assert - Should return all dependents regardless of enabled status
        canDisable.Should().BeFalse("dependents exist in registry");
        dependents.Should().HaveCount(2);
        dependents.Should().Contain("DependentModule1");
        dependents.Should().Contain("DependentModule2");
    }
    
    public void Dispose()
    {
        moduleManager?.Dispose();
        serviceProvider?.Dispose();
    }
    
    // Test helper classes
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
}
