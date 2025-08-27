using System;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SamplePlugin.Core.Module;
using Xunit;
using Dalamud.Plugin.Services;

namespace SamplePlugin.Tests.Core.Module;

public class ModuleRegistryTests
{
    private readonly ModuleRegistry registry;
    private readonly Mock<IPluginLog> mockLogger;
    
    public ModuleRegistryTests()
    {
        mockLogger = new Mock<IPluginLog>();
        registry = new ModuleRegistry(mockLogger.Object);
    }
    
    [Fact]
    public void DiscoverModules_ShouldFindAllModulesWithAttribute()
    {
        // Act
        registry.DiscoverModules();
        
        // Assert
        registry.RegisteredModules.Should().NotBeEmpty();
        registry.ModuleInfos.Should().NotBeEmpty();
        
        // Should find at least the Chat and ChatAnalyzer modules
        registry.RegisteredModules.Should().ContainKey("Chat");
        registry.RegisteredModules.Should().ContainKey("ChatAnalyzer");
    }
    
    [Fact]
    public void DiscoverModules_ShouldExtractModuleInfo()
    {
        // Act
        registry.DiscoverModules();
        
        // Assert
        var chatInfo = registry.ModuleInfos["Chat"];
        chatInfo.Should().NotBeNull();
        chatInfo.Name.Should().Be("Chat");
        chatInfo.Version.Should().Be("1.0.0");
        chatInfo.Author.Should().Be("Sample Author");
        chatInfo.Description.Should().Be("Chat monitoring and filtering module");
        
        var analyzerInfo = registry.ModuleInfos["ChatAnalyzer"];
        analyzerInfo.Should().NotBeNull();
        analyzerInfo.Dependencies.Should().Contain("Chat");
    }
    
    [Fact]
    public void RegisterModule_WithValidModule_ShouldRegisterSuccessfully()
    {
        // Arrange
        var moduleType = typeof(TestModuleWithAttribute);
        var info = new ModuleInfoAttribute("TestModule", "1.0.0")
        {
            Description = "Test module",
            Author = "Test Author"
        };
        
        // Act
        registry.RegisterModule("TestModule", moduleType, info);
        
        // Assert
        registry.RegisteredModules.Should().ContainKey("TestModule");
        registry.ModuleInfos.Should().ContainKey("TestModule");
        registry.RegisteredModules["TestModule"].Should().Be(moduleType);
    }
    
    [Fact]
    public void RegisterModule_DuplicateName_ShouldLogWarning()
    {
        // Arrange
        var moduleType1 = typeof(TestModuleWithAttribute);
        var moduleType2 = typeof(TestModuleWithDependency);
        var info = new ModuleInfoAttribute("TestModule", "1.0.0");
        
        // Act
        registry.RegisterModule("TestModule", moduleType1, info);
        registry.RegisterModule("TestModule", moduleType2, info);
        
        // Assert
        registry.RegisteredModules["TestModule"].Should().Be(moduleType2); // The second one replaces the first
        mockLogger.Verify(x => x.Warning(It.IsAny<string>()), Times.Once);
    }
    
    [Fact]
    public void ValidateDependencies_WithSatisfiedDependencies_ShouldReturnTrue()
    {
        // Arrange
        registry.RegisterModule("Module1", typeof(TestModuleWithAttribute), 
            new ModuleInfoAttribute("Module1", "1.0.0"));
        registry.RegisterModule("Module2", typeof(TestModuleWithDependency), 
            new ModuleInfoAttribute("Module2", "1.0.0") { Dependencies = ["Module1"] });
        
        // Act
        var result = registry.ValidateDependencies();
        
        // Assert
        result.Should().BeTrue();
    }
    
    [Fact]
    public void ValidateDependencies_WithMissingDependency_ShouldReturnFalse()
    {
        // Arrange
        registry.RegisterModule("Module2", typeof(TestModuleWithDependency), 
            new ModuleInfoAttribute("Module2", "1.0.0") { Dependencies = ["NonExistentModule"] });
        
        // Act
        var result = registry.ValidateDependencies();
        
        // Assert
        result.Should().BeFalse();
        mockLogger.Verify(x => x.Error(It.IsAny<string>()), Times.Once);
    }
    
    [Fact]
    public void ValidateDependencies_WithCircularDependency_ShouldReturnTrue()
    {
        // Arrange
        // Note: ValidateDependencies only checks if dependencies exist, not for circular dependencies
        registry.RegisterModule("Module1", typeof(TestModuleWithAttribute), 
            new ModuleInfoAttribute("Module1", "1.0.0") { Dependencies = ["Module2"] });
        registry.RegisterModule("Module2", typeof(TestModuleWithDependency), 
            new ModuleInfoAttribute("Module2", "1.0.0") { Dependencies = ["Module1"] });
        
        // Act
        var result = registry.ValidateDependencies();
        
        // Assert
        result.Should().BeTrue(); // Both dependencies exist, so validation passes
        mockLogger.Verify(x => x.Error(It.IsAny<string>()), Times.Never);
    }
    
    [Fact]
    public void GetModulesInLoadOrder_WithNoDependencies_ShouldReturnAll()
    {
        // Arrange
        registry.RegisterModule("Module1", typeof(TestModuleWithAttribute), 
            new ModuleInfoAttribute("Module1", "1.0.0"));
        registry.RegisterModule("Module2", typeof(TestModuleWithAttribute), 
            new ModuleInfoAttribute("Module2", "1.0.0"));
        
        // Act
        var loadOrder = registry.GetModulesInLoadOrder().ToList(); // Convert to list to avoid multiple enumeration
        
        // Assert
        loadOrder.Should().HaveCount(2);
        loadOrder.Should().Contain("Module1");
        loadOrder.Should().Contain("Module2");
    }
    
    [Fact]
    public void GetModulesInLoadOrder_WithDependencies_ShouldReturnCorrectOrder()
    {
        // Arrange
        registry.RegisterModule("Module1", typeof(TestModuleWithAttribute), 
            new ModuleInfoAttribute("Module1", "1.0.0"));
        registry.RegisterModule("Module2", typeof(TestModuleWithDependency), 
            new ModuleInfoAttribute("Module2", "1.0.0") { Dependencies = ["Module1"] });
        registry.RegisterModule("Module3", typeof(TestModuleWithAttribute), 
            new ModuleInfoAttribute("Module3", "1.0.0") { Dependencies = ["Module2"] });
        
        // Act
        var loadOrder = registry.GetModulesInLoadOrder().ToList(); // Convert to list to avoid multiple enumeration
        
        // Assert
        loadOrder.Should().HaveCount(3);
        loadOrder.Should().ContainInOrder("Module1", "Module2", "Module3");
    }
    
    [Fact]
    public void GetModulesInLoadOrder_WithPriority_ShouldRespectPriority()
    {
        // Arrange
        registry.RegisterModule("Module1", typeof(TestModuleWithAttribute), 
            new ModuleInfoAttribute("Module1", "1.0.0") { Priority = 10 });
        registry.RegisterModule("Module2", typeof(TestModuleWithAttribute), 
            new ModuleInfoAttribute("Module2", "1.0.0") { Priority = 5 });
        registry.RegisterModule("Module3", typeof(TestModuleWithAttribute), 
            new ModuleInfoAttribute("Module3", "1.0.0") { Priority = 15 });
        
        // Act
        var loadOrder = registry.GetModulesInLoadOrder().ToList(); // Convert to list to avoid multiple enumeration
        
        // Assert
        loadOrder.Should().HaveCount(3);
        // Lower priority loads first
        loadOrder.Should().ContainInOrder("Module2", "Module1", "Module3");
    }
    
    [Fact]
    public void CreateModuleInstance_WithValidModule_ShouldCreateInstance()
    {
        // Arrange
        registry.RegisterModule("TestModule", typeof(TestModuleWithAttribute), 
            new ModuleInfoAttribute("TestModule", "1.0.0"));
        
        // Act
        var instance = registry.CreateModuleInstance("TestModule");
        
        // Assert
        instance.Should().NotBeNull();
        instance.Should().BeOfType<TestModuleWithAttribute>();
        instance!.Name.Should().Be("TestModule");
    }
    
    [Fact]
    public void CreateModuleInstance_WithNonExistentModule_ShouldReturnNull()
    {
        // Act
        var instance = registry.CreateModuleInstance("NonExistentModule");
        
        // Assert
        instance.Should().BeNull();
        mockLogger.Verify(x => x.Warning(It.IsAny<string>()), Times.Once);
    }
    
    [Fact]
    public void CreateModuleInstance_WithInvalidConstructor_ShouldReturnNull()
    {
        // Arrange
        registry.RegisterModule("BadModule", typeof(TestModuleWithBadConstructor), 
            new ModuleInfoAttribute("BadModule", "1.0.0"));
        
        // Act
        var instance = registry.CreateModuleInstance("BadModule");
        
        // Assert
        instance.Should().BeNull();
        mockLogger.Verify(x => x.Error(It.IsAny<Exception>(), It.IsAny<string>()), Times.Once);
    }
    
    // Test helper classes
    [ModuleInfo("TestModule", "1.0.0")]
    private class TestModuleWithAttribute : ModuleBase
    {
        public override string Name => "TestModule";
        public override string Version => "1.0.0";
        public override void RegisterServices(IServiceCollection services) { }
    }
    
    [ModuleInfo("DependentModule", "1.0.0", Dependencies = ["TestModule"])]
    private class TestModuleWithDependency : ModuleBase
    {
        public override string Name => "DependentModule";
        public override string Version => "1.0.0";
        public override string[] Dependencies => ["TestModule"];
        public override void RegisterServices(IServiceCollection services) { }
    }
    
    private class TestModuleWithBadConstructor : ModuleBase
    {
        private readonly string requiredParameter;
        
        public TestModuleWithBadConstructor(string required) // Requires parameter
        {
            // Store the parameter to show it's necessary for the constructor
            requiredParameter = required ?? throw new ArgumentNullException(nameof(required));
            
            // Still throw to simulate a bad constructor for testing purposes
            throw new NotImplementedException($"Bad constructor with required parameter: {requiredParameter}");
        }
        
        public override string Name => "BadModule";
        public override string Version => "1.0.0";
        public override void RegisterServices(IServiceCollection services) { }
    }
}
