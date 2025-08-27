using System;
using System.Collections.Generic;
using System.Text.Json;
using Dalamud.Configuration;
using Dalamud.Plugin;

namespace SamplePlugin.Core.Configuration;

[Serializable]
public class PluginConfiguration : IPluginConfiguration, IConfiguration
{
    public int Version { get; set; } = 1;
    
    private Dictionary<string, JsonElement> settings = new();
    private IDalamudPluginInterface? pluginInterface;
    
    public void Initialize(IDalamudPluginInterface dalamudPluginInterface)
    {
        this.pluginInterface = dalamudPluginInterface;
        Load();
    }
    
    public T Get<T>(string key, T defaultValue = default!)
    {
        if (!settings.TryGetValue(key, out var element))
            return defaultValue;
        
        try
        {
            var json = element.GetRawText();
            var result = JsonSerializer.Deserialize<T>(json);
            return result ?? defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }
    
    public void Set<T>(string key, T value)
    {
        var json = JsonSerializer.Serialize(value);
        settings[key] = JsonDocument.Parse(json).RootElement.Clone();
    }
    
    public void Save()
    {
        pluginInterface?.SavePluginConfig(this);
    }
    
    public void Load()
    {
        var config = pluginInterface?.GetPluginConfig();
        if (config is PluginConfiguration pluginConfig)
        {
            settings = pluginConfig.settings;
            Version = pluginConfig.Version;
        }
    }
    
    public void Reset()
    {
        settings.Clear();
        Save();
    }
    
    // Module-specific configuration helpers
    public ModuleConfiguration GetModuleConfig(string moduleName)
    {
        var key = $"Module.{moduleName}";
        return Get(key, new ModuleConfiguration { ModuleName = moduleName });
    }
    
    public void SetModuleConfig(string moduleName, ModuleConfiguration config)
    {
        var key = $"Module.{moduleName}";
        Set(key, config);
    }
    
    /// <summary>
    /// Gets all module configurations
    /// </summary>
    public Dictionary<string, ModuleConfiguration> GetAllModuleConfigs()
    {
        var configs = new Dictionary<string, ModuleConfiguration>();
        
        foreach (var kvp in settings)
        {
            if (kvp.Key.StartsWith("Module."))
            {
                var moduleName = kvp.Key[7..]; // Remove "Module." prefix
                try
                {
                    var json = kvp.Value.GetRawText();
                    var config = JsonSerializer.Deserialize<ModuleConfiguration>(json);
                    if (config != null)
                    {
                        configs[moduleName] = config;
                    }
                }
                catch
                {
                    // Skip invalid configs
                }
            }
        }
        
        return configs;
    }
    
    /// <summary>
    /// Removes a module configuration
    /// </summary>
    public void RemoveModuleConfig(string moduleName)
    {
        var key = $"Module.{moduleName}";
        settings.Remove(key);
    }
}

public class ModuleConfiguration
{
    public string ModuleName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public Dictionary<string, JsonElement> Settings { get; set; } = new();
    
    public T GetSetting<T>(string key, T defaultValue = default!)
    {
        if (!Settings.TryGetValue(key, out var element))
            return defaultValue;
        
        try
        {
            var json = element.GetRawText();
            var result = JsonSerializer.Deserialize<T>(json);
            return result ?? defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }
    
    public void SetSetting<T>(string key, T value)
    {
        var json = JsonSerializer.Serialize(value);
        Settings[key] = JsonDocument.Parse(json).RootElement.Clone();
    }
}
