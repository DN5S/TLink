using TLink.Core.Configuration;

namespace TLink.Modules.Translation.Configuration;

/// <summary>
/// Configuration for the Translation orchestrator module.
/// Only contains orchestrator-level settings.
/// Individual handlers have their own configuration.
/// </summary>
public class TranslationConfig : ModuleConfiguration
{
    // --- Pipeline Settings ---
    public string SourceLanguage { get; set; } = "auto";
    public string TargetLanguage { get; set; } = "en";
    
    // --- Execution Settings ---
    public int PipelineTimeoutMs { get; set; } = 5000;
    public bool EnablePipelineMetrics { get; set; } = true;
    
    public TranslationConfig()
    {
        ModuleName = "Translation";
    }
}
