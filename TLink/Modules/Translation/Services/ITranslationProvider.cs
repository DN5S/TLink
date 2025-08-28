using System.Threading.Tasks;

namespace TLink.Modules.Translation.Services;

/// <summary>
/// Contract that all translation provider modules must implement.
/// This interface defines the standard for translation services.
/// </summary>
public interface ITranslationProvider
{
    /// <summary>
    /// The display name of this provider.
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Indicates whether this provider can preserve XML/HTML tags during translation.
    /// If true, the provider can handle SeString XML encoding.
    /// If false, only plain text will be sent.
    /// </summary>
    bool SupportsXmlTags { get; }
    
    /// <summary>
    /// Indicates whether this provider is currently available and configured.
    /// </summary>
    bool IsAvailable { get; }
    
    /// <summary>
    /// Translates text from source language to target language.
    /// </summary>
    /// <param name="text">The text to translate (may contain XML tags if SupportsXmlTags is true)</param>
    /// <param name="sourceLang">Source language code (e.g., "auto", "en", "ja", "ko")</param>
    /// <param name="targetLang">Target language code (e.g., "en", "ja", "ko")</param>
    /// <returns>Translated text</returns>
    Task<string> TranslateAsync(string text, string sourceLang, string targetLang);
    
    /// <summary>
    /// Validates provider configuration and connectivity.
    /// </summary>
    /// <returns>True if the provider is properly configured and reachable</returns>
    Task<bool> ValidateAsync();
}
