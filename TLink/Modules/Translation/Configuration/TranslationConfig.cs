using TLink.Core.Configuration;

namespace TLink.Modules.Translation.Configuration;

public class TranslationConfig : ModuleConfiguration
{
// --- Hub Core Settings ---
    public string ActiveProvider { get; set; } = string.Empty;
    
    // --- Engine Settings ---
    public string SourceLanguage { get; set; } = "auto";
    public string TargetLanguage { get; set; } = "en";
    
    // --- Performance Settings ---
    public bool EnableCache { get; set; } = true;
    public int CacheSize { get; set; } = 100;
    
    public TranslationConfig()
    {
        ModuleName = "Translation";
    }
}
