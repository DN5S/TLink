using System;
using System.Collections.Generic;
using System.Text.Json;
using FluentAssertions;
using Moq;
using SamplePlugin.Core.Configuration;
using Xunit;
using Dalamud.Plugin;

namespace SamplePlugin.Tests.Core.Configuration;

public class PluginConfigurationTests : IDisposable
{
    private readonly Mock<IDalamudPluginInterface> mockPluginInterface;
    private readonly PluginConfiguration configuration;
    
    public PluginConfigurationTests()
    {
        mockPluginInterface = new Mock<IDalamudPluginInterface>();
        configuration = new PluginConfiguration();
        configuration.Initialize(mockPluginInterface.Object);
    }
    
    [Fact]
    public void Get_WithNonExistentKey_ShouldReturnDefault()
    {
        // Act
        var result = configuration.Get("NonExistent", "default");
        
        // Assert
        result.Should().Be("default");
    }
    
    [Fact]
    public void Set_AndGet_ShouldStoreAndRetrieveValue()
    {
        // Arrange
        const string key = "TestKey";
        const string value = "TestValue";
        
        // Act
        configuration.Set(key, value);
        var result = configuration.Get<string>(key);
        
        // Assert
        result.Should().Be(value);
    }
    
    [Fact]
    public void Set_AndGet_WithComplexObject_ShouldWork()
    {
        // Arrange
        var testObject = new TestObject
        {
            Name = "Test",
            Value = 42,
            Items = ["Item1", "Item2"]
        };
        
        // Act
        configuration.Set("ComplexObject", testObject);
        var result = configuration.Get<TestObject>("ComplexObject");
        
        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Test");
        result.Value.Should().Be(42);
        result.Items.Should().ContainInOrder("Item1", "Item2");
    }
    
    [Fact]
    public void GetModuleConfig_WithNonExistentModule_ShouldReturnDefault()
    {
        // Act
        var config = configuration.GetModuleConfig("TestModule");
        
        // Assert
        config.Should().NotBeNull();
        config.ModuleName.Should().Be("TestModule");
        config.IsEnabled.Should().BeTrue(); // Default value
        config.Settings.Should().BeEmpty();
    }
    
    [Fact]
    public void SetModuleConfig_AndGetModuleConfig_ShouldWork()
    {
        // Arrange
        var moduleConfig = new ModuleConfiguration
        {
            ModuleName = "TestModule",
            IsEnabled = false,
            Settings = new Dictionary<string, JsonElement>()
        };
        moduleConfig.SetSetting("TestSetting", "TestValue");
        
        // Act
        configuration.SetModuleConfig("TestModule", moduleConfig);
        var retrievedConfig = configuration.GetModuleConfig("TestModule");
        
        // Assert
        retrievedConfig.Should().NotBeNull();
        retrievedConfig.ModuleName.Should().Be("TestModule");
        retrievedConfig.IsEnabled.Should().BeFalse();
        retrievedConfig.GetSetting<string>("TestSetting").Should().Be("TestValue");
    }
    
    [Fact]
    public void GetAllModuleConfigs_ShouldReturnAllModuleConfigs()
    {
        // Arrange
        var config1 = new ModuleConfiguration { ModuleName = "Module1", IsEnabled = true };
        var config2 = new ModuleConfiguration { ModuleName = "Module2", IsEnabled = false };
        
        configuration.SetModuleConfig("Module1", config1);
        configuration.SetModuleConfig("Module2", config2);
        configuration.Set("NonModuleKey", "SomeValue"); // Should be ignored
        
        // Act
        var allConfigs = configuration.GetAllModuleConfigs();
        
        // Assert
        allConfigs.Should().HaveCount(2);
        allConfigs.Should().ContainKey("Module1");
        allConfigs.Should().ContainKey("Module2");
        allConfigs["Module1"].IsEnabled.Should().BeTrue();
        allConfigs["Module2"].IsEnabled.Should().BeFalse();
    }
    
    [Fact]
    public void RemoveModuleConfig_ShouldRemoveConfig()
    {
        // Arrange
        var moduleConfig = new ModuleConfiguration { ModuleName = "TestModule" };
        configuration.SetModuleConfig("TestModule", moduleConfig);
        
        // Act
        configuration.RemoveModuleConfig("TestModule");
        var retrievedConfig = configuration.GetModuleConfig("TestModule");
        
        // Assert
        retrievedConfig.ModuleName.Should().Be("TestModule");
        retrievedConfig.Settings.Should().BeEmpty(); // Should return default
    }
    
    [Fact]
    public void Save_ShouldCallPluginInterfaceSave()
    {
        // Act
        configuration.Save();
        
        // Assert
        mockPluginInterface.Verify(x => x.SavePluginConfig(configuration), Times.Once);
    }
    
    [Fact]
    public void Load_WithExistingConfig_ShouldLoadSettings()
    {
        // Arrange
        var existingConfig = new PluginConfiguration();
        existingConfig.Set("ExistingKey", "ExistingValue");
        mockPluginInterface.Setup(x => x.GetPluginConfig()).Returns(existingConfig);
        
        // Act
        configuration.Load();
        var value = configuration.Get<string>("ExistingKey");
        
        // Assert
        value.Should().Be("ExistingValue");
    }
    
    [Fact]
    public void Reset_ShouldClearAllSettings()
    {
        // Arrange
        configuration.Set("Key1", "Value1");
        configuration.Set("Key2", "Value2");
        
        // Act
        configuration.Reset();
        
        // Assert
        configuration.Get<string>("Key1").Should().BeNull();
        configuration.Get<string>("Key2").Should().BeNull();
        mockPluginInterface.Verify(x => x.SavePluginConfig(configuration), Times.Once);
    }
    
    [Fact]
    public void Version_ShouldHaveDefaultValue()
    {
        // Assert
        configuration.Version.Should().Be(1);
    }
    
    [Fact]
    public void ModuleConfiguration_GetSetting_WithNonExistentKey_ShouldReturnDefault()
    {
        // Arrange
        var moduleConfig = new ModuleConfiguration();
        
        // Act
        var result = moduleConfig.GetSetting("NonExistent", "default");
        
        // Assert
        result.Should().Be("default");
    }
    
    [Fact]
    public void ModuleConfiguration_SetSetting_AndGetSetting_ShouldWork()
    {
        // Arrange
        var moduleConfig = new ModuleConfiguration();
        
        // Act
        moduleConfig.SetSetting("TestKey", 123);
        var result = moduleConfig.GetSetting<int>("TestKey");
        
        // Assert
        result.Should().Be(123);
    }
    
    [Fact]
    public void ModuleConfiguration_SetSetting_WithComplexObject_ShouldWork()
    {
        // Arrange
        var moduleConfig = new ModuleConfiguration();
        var testObject = new TestObject
        {
            Name = "ModuleTest",
            Value = 99,
            Items = ["A", "B", "C"]
        };
        
        // Act
        moduleConfig.SetSetting("ComplexSetting", testObject);
        var result = moduleConfig.GetSetting<TestObject>("ComplexSetting");
        
        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("ModuleTest");
        result.Value.Should().Be(99);
        result.Items.Should().ContainInOrder("A", "B", "C");
    }
    
    [Fact]
    public void Get_WithInvalidJsonDeserialization_ShouldReturnDefault()
    {
        // Arrange
        configuration.Set("BadData", "This is not valid JSON for TestObject");
        
        // Act
        var result = configuration.Get("BadData", new TestObject { Name = "Default" });
        
        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Default");
    }
    
    public void Dispose()
    {
        // Cleanup if needed
    }
    
    // Test helper class
    private class TestObject
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
        public List<string> Items { get; set; } = [];
    }
}
