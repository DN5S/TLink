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

public class ModuleBaseTests : IDisposable
{
    private readonly ServiceProvider serviceProvider;
    private readonly Mock<IDalamudPluginInterface> mockPluginInterface;
    private readonly Mock<IPluginLog> mockLogger;
    private readonly EventBus eventBus;
    
    public ModuleBaseTests()
    {
        mockPluginInterface = new Mock<IDalamudPluginInterface>();
        mockLogger = new Mock<IPluginLog>();
        eventBus = new EventBus();
        
        var services = new ServiceCollection();
        services.AddSingleton(mockPluginInterface.Object);
        services.AddSingleton(mockLogger.Object);
        services.AddSingleton(eventBus);
        
        // Add PluginConfiguration, which is now required by ModuleBase
        var configuration = new PluginConfiguration();
        configuration.Initialize(mockPluginInterface.Object);
        services.AddSingleton(configuration);
        
        serviceProvider = services.BuildServiceProvider();
    }
    
    [Fact]
    public void InjectDependencies_ShouldSetAllRequiredServices()
    {
        // Arrange
        var module = new TestModuleBase();
        
        // Act
        module.InjectDependencies(serviceProvider);
        
        // Assert
        module.GetPluginInterface().Should().NotBeNull();
        module.GetLogger().Should().NotBeNull();
        module.GetEventBus().Should().NotBeNull();
        module.GetServices().Should().NotBeNull();
    }
    
    [Fact]
    public void Name_ShouldReturnCorrectValue()
    {
        // Arrange
        var module = new TestModuleBase();
        
        // Assert
        module.Name.Should().Be("TestModule");
    }
    
    [Fact]
    public void Version_ShouldReturnCorrectValue()
    {
        // Arrange
        var module = new TestModuleBase();
        
        // Assert
        module.Version.Should().Be("1.0.0");
    }
    
    [Fact]
    public void Dependencies_ShouldReturnEmptyByDefault()
    {
        // Arrange
        var module = new TestModuleBase();
        
        // Assert
        module.Dependencies.Should().BeEmpty();
    }
    
    [Fact]
    public void Initialize_ShouldBeCallable()
    {
        // Arrange
        var module = new TestModuleBase();
        module.InjectDependencies(serviceProvider);
        
        // Act & Assert - Should not throw
        module.Initialize();
        module.InitializeCalled.Should().BeTrue();
    }
    
    [Fact]
    public void DrawUI_ShouldBeCallable()
    {
        // Arrange
        var module = new TestModuleBase();
        
        // Act & Assert - Should not throw
        module.DrawUI();
        module.DrawUICalled.Should().BeTrue();
    }
    
    [Fact]
    public void DrawConfiguration_ShouldBeCallable()
    {
        // Arrange
        var module = new TestModuleBase();
        
        // Act & Assert - Should not throw
        module.DrawConfiguration();
        module.DrawConfigurationCalled.Should().BeTrue();
    }
    
    [Fact]
    public void Dispose_ShouldDisposeSubscriptions()
    {
        // Arrange
        var module = new TestModuleBase();
        module.InjectDependencies(serviceProvider);
        
        var messageReceived = false;
        module.AddSubscription(
            eventBus.Listen<TestMessage>()
                .Subscribe(_ => messageReceived = true)
        );
        
        // Act
        module.Dispose();
        var testMessage = new TestMessage { Content = "Test Content" };
        eventBus.Publish(testMessage);
        
        // Assert
        messageReceived.Should().BeFalse("Subscription should be disposed");
        module.IsDisposed.Should().BeTrue();
        testMessage.Content.Should().Be("Test Content", "Verifying message content property works correctly");
    }
    
    [Fact]
    public void Dispose_MultipleTimes_ShouldNotThrow()
    {
        // Arrange
        var module = new TestModuleBase();
        
        // Act & Assert
        module.Dispose();
        module.Dispose(); // Should not throw
    }
    
    [Fact]
    public void ModuleWithDependencies_ShouldReturnCorrectDependencies()
    {
        // Arrange
        var module = new TestModuleWithDependencies();
        
        // Assert
        module.Dependencies.Should().ContainInOrder("Module1", "Module2");
    }
    
    public void Dispose()
    {
        eventBus?.Dispose();
        serviceProvider?.Dispose();
    }
    
    // Test helper classes
    private class TestModuleBase : ModuleBase
    {
        public override string Name => "TestModule";
        public override string Version => "1.0.0";
        
        public bool InitializeCalled { get; private set; }
        public bool DrawUICalled { get; private set; }
        public bool DrawConfigurationCalled { get; private set; }
        public bool IsDisposed { get; private set; }
        
        public override void RegisterServices(IServiceCollection services)
        {
            // Empty implementation for testing
        }
        
        public override void Initialize()
        {
            base.Initialize();
            InitializeCalled = true;
        }
        
        public override void DrawUI()
        {
            base.DrawUI();
            DrawUICalled = true;
        }
        
        public override void DrawConfiguration()
        {
            base.DrawConfiguration();
            DrawConfigurationCalled = true;
        }
        
        public override void Dispose()
        {
            IsDisposed = true;
            base.Dispose();
        }
        
        // Expose protected members for testing
        public IDalamudPluginInterface GetPluginInterface() => PluginInterface;
        public IPluginLog GetLogger() => Logger;
        public EventBus GetEventBus() => EventBus;
        public IServiceProvider GetServices() => Services;
        
        public void AddSubscription(IDisposable subscription)
        {
            Subscriptions.Add(subscription);
        }
    }
    
    private class TestModuleWithDependencies : ModuleBase
    {
        public override string Name => "DependentModule";
        public override string Version => "1.0.0";
        public override string[] Dependencies => ["Module1", "Module2"];
        
        public override void RegisterServices(IServiceCollection services)
        {
            // Empty implementation for testing
        }
    }
    
    private class TestMessage
    {
        public string Content { get; set; } = string.Empty;
    }
}
