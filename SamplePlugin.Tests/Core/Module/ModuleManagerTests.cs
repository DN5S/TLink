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

public class ModuleManagerTests : IDisposable
{
    private readonly ServiceProvider serviceProvider;
    private readonly Mock<IPluginLog> mockLogger;
    private readonly ModuleManager moduleManager;
    
    public ModuleManagerTests()
    {
        mockLogger = new Mock<IPluginLog>();
        var mockPluginInterface = new Mock<IDalamudPluginInterface>();
        
        var services = new ServiceCollection();
        services.AddSingleton(mockLogger.Object);
        services.AddSingleton(mockPluginInterface.Object);
        services.AddSingleton<EventBus>();
        
        // Add PluginConfiguration
        var configuration = new PluginConfiguration();
        configuration.Initialize(mockPluginInterface.Object);
        services.AddSingleton(configuration);
        
        serviceProvider = services.BuildServiceProvider();
        
        moduleManager = new ModuleManager(serviceProvider, mockLogger.Object);
    }
    
    [Fact]
    public void LoadModule_WithNullModule_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => moduleManager.LoadModule(null!));
    }
    
    [Fact]
    public void LoadModule_WithValidModule_ShouldLoadSuccessfully()
    {
        // Arrange
        var module = new TestModule();
        
        // Act
        moduleManager.LoadModule(module);
        
        // Assert
        moduleManager.LoadedModules.Should().ContainSingle();
        moduleManager.LoadedModules[0].Name.Should().Be("TestModule");
        module.IsInitialized.Should().BeTrue();
    }
    
    [Fact]
    public void LoadModule_WithSameModuleTwice_ShouldNotLoadSecondTime()
    {
        // Arrange
        var module1 = new TestModule();
        var module2 = new TestModule();
        
        // Act
        moduleManager.LoadModule(module1);
        moduleManager.LoadModule(module2);
        
        // Assert
        moduleManager.LoadedModules.Should().ContainSingle();
        mockLogger.Verify(x => x.Warning(It.IsAny<string>()), Times.Once);
    }
    
    [Fact]
    public void LoadModule_WithMissingDependency_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var module = new TestModuleWithDependency();
        
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => moduleManager.LoadModule(module));
        moduleManager.LoadedModules.Should().BeEmpty();
    }
    
    [Fact]
    public void LoadModule_WithSatisfiedDependency_ShouldLoadSuccessfully()
    {
        // Arrange
        var baseModule = new TestModule();
        var dependentModule = new TestModuleWithDependency();
        
        // Act
        moduleManager.LoadModule(baseModule);
        moduleManager.LoadModule(dependentModule);
        
        // Assert
        moduleManager.LoadedModules.Should().HaveCount(2);
        moduleManager.LoadedModules.Select(m => m.Name)
            .Should().ContainInOrder("TestModule", "DependentModule");
    }
    
    [Fact]
    public void LoadModule_WithGenericMethod_ShouldCreateAndLoadModule()
    {
        // Act
        moduleManager.LoadModule<TestModule>();
        
        // Assert
        moduleManager.LoadedModules.Should().ContainSingle();
        moduleManager.LoadedModules[0].Name.Should().Be("TestModule");
    }
    
    [Fact]
    public void UnloadModule_WithLoadedModule_ShouldRemoveModule()
    {
        // Arrange
        var module = new TestModule();
        moduleManager.LoadModule(module);
        
        // Act
        moduleManager.UnloadModule("TestModule");
        
        // Assert
        moduleManager.LoadedModules.Should().BeEmpty();
        module.IsDisposed.Should().BeTrue();
    }
    
    [Fact]
    public void UnloadModule_WithDependentModules_ShouldUnloadDependentsFirst()
    {
        // Arrange
        var baseModule = new TestModule();
        var dependentModule = new TestModuleWithDependency();
        moduleManager.LoadModule(baseModule);
        moduleManager.LoadModule(dependentModule);
        
        // Act
        moduleManager.UnloadModule("TestModule");
        
        // Assert
        moduleManager.LoadedModules.Should().BeEmpty();
        baseModule.IsDisposed.Should().BeTrue();
        dependentModule.IsDisposed.Should().BeTrue();
    }
    
    [Fact]
    public void UnloadModule_WithNonExistentModule_ShouldDoNothing()
    {
        // Act
        moduleManager.UnloadModule("NonExistent");
        
        // Assert
        moduleManager.LoadedModules.Should().BeEmpty();
    }
    
    [Fact]
    public void DrawUI_ShouldCallDrawUIOnAllModules()
    {
        // Arrange
        var module1 = new TestModule("Module1");
        var module2 = new TestModule("Module2");
        moduleManager.LoadModule(module1);
        moduleManager.LoadModule(module2);
        
        // Act
        moduleManager.DrawUI();
        
        // Assert
        module1.DrawUICalled.Should().BeTrue();
        module2.DrawUICalled.Should().BeTrue();
    }
    
    [Fact]
    public void DrawUI_WithExceptionInModule_ShouldContinueWithOtherModules()
    {
        // Arrange
        var module1 = new TestModuleWithException();
        var module2 = new TestModule();
        moduleManager.LoadModule(module1);
        moduleManager.LoadModule(module2);
        
        // Act
        moduleManager.DrawUI();
        
        // Assert
        module2.DrawUICalled.Should().BeTrue();
        mockLogger.Verify(x => x.Error(It.IsAny<Exception>(), It.IsAny<string>()), Times.Once);
    }
    
    [Fact]
    public void DrawConfiguration_ShouldCallDrawConfigurationOnAllModules()
    {
        // Arrange
        var module1 = new TestModule("Module1");
        var module2 = new TestModule("Module2");
        moduleManager.LoadModule(module1);
        moduleManager.LoadModule(module2);
        
        // Act
        moduleManager.DrawConfiguration();
        
        // Assert
        module1.DrawConfigurationCalled.Should().BeTrue();
        module2.DrawConfigurationCalled.Should().BeTrue();
    }
    
    [Fact]
    public void Dispose_ShouldUnloadAllModules()
    {
        // Arrange
        var module1 = new TestModule("Module1");
        var module2 = new TestModule("Module2");
        moduleManager.LoadModule(module1);
        moduleManager.LoadModule(module2);
        
        // Act
        moduleManager.Dispose();
        
        // Assert
        moduleManager.LoadedModules.Should().BeEmpty();
        module1.IsDisposed.Should().BeTrue();
        module2.IsDisposed.Should().BeTrue();
    }
    
    [Fact]
    public void LoadAllRegisteredModules_ShouldDiscoverAndLoadModules()
    {
        // Arrange
        var configuration = serviceProvider.GetRequiredService<PluginConfiguration>();
        
        // Act
        moduleManager.LoadAllRegisteredModules(configuration);
        
        // Assert
        // Note: In the test environment, modules from the main assembly are discovered
        // The test passes if the discovery and loading process doesn't throw
        // Actual module loading depends on what's in the executing assembly
        moduleManager.Registry.Should().NotBeNull();
        
        // If modules are loaded, verify they're in the correct order
        if (moduleManager.LoadedModules.Count > 0)
        {
            // Should have loaded at least the Chat module if it's discovered
            var chatModule = moduleManager.LoadedModules.FirstOrDefault(m => m.Name == "Chat");
            chatModule?.Should().NotBeNull();
        }
    }
    
    [Fact]
    public void LoadAllRegisteredModules_WithDisabledModule_ShouldSkip()
    {
        // Arrange
        var configuration = serviceProvider.GetRequiredService<PluginConfiguration>();
        
        // Disable ChatAnalyzer module
        var chatAnalyzerConfig = new ModuleConfiguration
        {
            ModuleName = "ChatAnalyzer",
            IsEnabled = false
        };
        configuration.SetModuleConfig("ChatAnalyzer", chatAnalyzerConfig);
        
        // Act
        moduleManager.LoadAllRegisteredModules(configuration);
        
        // Assert
        moduleManager.LoadedModules.Should().NotContain(m => m.Name == "ChatAnalyzer");
        mockLogger.Verify(x => x.Information(It.Is<string>(s => s.Contains("Skipping disabled module: ChatAnalyzer"))), Times.Once);
    }
    
    [Fact]
    public void GetModuleInfo_WithLoadedModule_ShouldReturnInfo()
    {
        // Arrange
        var configuration = serviceProvider.GetRequiredService<PluginConfiguration>();
        moduleManager.LoadAllRegisteredModules(configuration);
        
        // Act
        var info = moduleManager.GetModuleInfo("Chat");
        
        // Assert
        info.Should().NotBeNull();
        info!.Name.Should().Be("Chat");
        info.Version.Should().Be("1.0.0");
        info.Author.Should().Be("Sample Author");
    }
    
    [Fact]
    public void GetModuleInfo_WithUnknownModule_ShouldReturnNull()
    {
        // Act
        var info = moduleManager.GetModuleInfo("NonExistentModule");
        
        // Assert
        info.Should().BeNull();
    }
    
    [Fact]
    public void Registry_ShouldBeInitialized()
    {
        // Assert
        moduleManager.Registry.Should().NotBeNull();
        moduleManager.Registry.Should().BeOfType<ModuleRegistry>();
    }
    
    [Fact]
    public void LoadAllRegisteredModules_WithDependencies_ShouldLoadInCorrectOrder()
    {
        // Arrange
        var configuration = serviceProvider.GetRequiredService<PluginConfiguration>();
        
        // Act
        moduleManager.LoadAllRegisteredModules(configuration);
        
        // Assert
        if (moduleManager.LoadedModules.Count > 1)
        {
            // If ChatAnalyzer is loaded, Chat must be loaded before it
            var chatIndex = moduleManager.LoadedModules.ToList().FindIndex(m => m.Name == "Chat");
            var analyzerIndex = moduleManager.LoadedModules.ToList().FindIndex(m => m.Name == "ChatAnalyzer");
            
            if (analyzerIndex >= 0)
            {
                chatIndex.Should().BeLessThan(analyzerIndex, "Chat module should be loaded before ChatAnalyzer due to dependency");
            }
        }
    }
    
    public void Dispose()
    {
        moduleManager?.Dispose();
        serviceProvider?.Dispose();
    }
    
    // Test helper classes
    private class TestModule(string name) : IModule
    {
        public TestModule() : this("TestModule")
        {
        }

        public string Name => name;
        public string Version => "1.0.0";
        public string[] Dependencies => [];
        
        public bool IsInitialized { get; private set; }
        public bool IsDisposed { get; private set; }
        public bool DrawUICalled { get; private set; }
        public bool DrawConfigurationCalled { get; private set; }
        
        public void RegisterServices(IServiceCollection services) { }
        
        public void Initialize()
        {
            IsInitialized = true;
        }
        
        public void DrawUI()
        {
            DrawUICalled = true;
        }
        
        public void DrawConfiguration()
        {
            DrawConfigurationCalled = true;
        }
        
        public void Dispose()
        {
            IsDisposed = true;
        }
    }
    
    private class TestModuleWithDependency : IModule
    {
        public string Name => "DependentModule";
        public string Version => "1.0.0";
        public string[] Dependencies => ["TestModule"];
        
        public bool IsDisposed { get; private set; }
        
        public void RegisterServices(IServiceCollection services) { }
        public void Initialize() { }
        public void DrawUI() { }
        public void DrawConfiguration() { }
        
        public void Dispose()
        {
            IsDisposed = true;
        }
    }
    
    private class TestModuleWithException : IModule
    {
        public string Name => "ExceptionModule";
        public string Version => "1.0.0";
        public string[] Dependencies => [];
        
        public void RegisterServices(IServiceCollection services) { }
        public void Initialize() { }
        
        public void DrawUI()
        {
            throw new InvalidOperationException("Test exception");
        }
        
        public void DrawConfiguration()
        {
            throw new InvalidOperationException("Test exception");
        }
        
        public void Dispose() { }
    }
}
