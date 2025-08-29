using TLink.Core.Configuration;

namespace TLink.Modules.Translation.Providers.DeepL;

public class DeepLConfig : ModuleConfiguration
{
    public string ApiKey { get; set; } = string.Empty;
    
    public string ApiUrl { get; set; } = "https://api-free.deepl.com/v2/";
    
    public bool UsePro { get; set; }
    
    public bool PreserveFormatting { get; set; } = true;
    
    public int MaxRetries { get; set; } = 3;
    
    public int TimeoutMs { get; set; } = 10000;

    /// <summary>
    /// DeepL-handler-specific enabled state.
    /// Different from base ModuleConfiguration.IsEnabled which controls module loading.
    /// This controls whether the DeepL handler participates in the pipeline when loaded.
    /// </summary>
    public new bool IsEnabled { get; set; }
    
    public DeepLConfig()
    {
        ModuleName = "Translation.DeepL";
    }
    
    public string GetApiUrl()
    {
        return UsePro ? "https://api.deepl.com/v2/" : ApiUrl;
    }
    
    public bool IsConfigured()
    {
        return !string.IsNullOrWhiteSpace(ApiKey);
    }
}
