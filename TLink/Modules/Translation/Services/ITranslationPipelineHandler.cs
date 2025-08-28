using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TLink.Modules.Chat.Models;
using TLink.Modules.Translation.Models;

namespace TLink.Modules.Translation.Services;

/// <summary>
/// Represents a single step in the translation processing pipeline.
/// Each handler can inspect, modify, or terminate the pipeline flow.
/// </summary>
public interface ITranslationPipelineHandler
{
    /// <summary>
    /// The priority of this handler in the chain. Lower numbers execute first.
    /// Common priorities:
    /// - 0-20: Pre-processing (validation, rate limiting)
    /// - 20-50: Caching layers
    /// - 50-80: Enhancement middleware
    /// - 80-100: Translation providers
    /// - 100+: Post-processing
    /// </summary>
    int Priority { get; }
    
    /// <summary>
    /// The name of this handler for identification and logging.
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Whether this handler is currently enabled.
    /// </summary>
    bool IsEnabled { get; }
    
    /// <summary>
    /// Gets the list of ISO 639-1 language codes supported by this handler.
    /// </summary>
    IReadOnlyList<string> SupportedLanguages { get; }
    
    /// <summary>
    /// Handles the translation context.
    /// </summary>
    /// <param name="context">The data object flowing through the pipeline.</param>
    /// <param name="next">A function to call the next handler in the chain.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task HandleAsync(TranslationContext context, Func<Task> next);
}

/// <summary>
/// A state object that is passed through the translation pipeline.
/// Contains the original message, result, and metadata.
/// </summary>
public class TranslationContext
{
    /// <summary>
    /// The original chat message to translate.
    /// </summary>
    public ChatMessage OriginalMessage { get; }
    
    /// <summary>
    /// The translation result. Set by handlers that complete the translation.
    /// </summary>
    public TranslationResult? Result { get; set; }
    
    /// <summary>
    /// Indicates whether the translation has been handled.
    /// When true, further handlers may skip processing.
    /// </summary>
    public bool IsHandled { get; set; }
    
    /// <summary>
    /// Source language code (e.g., "ja", "de", "auto").
    /// </summary>
    public string SourceLanguage { get; set; }
    
    /// <summary>
    /// Target language code (e.g., "en", "fr").
    /// </summary>
    public string TargetLanguage { get; set; }
    
    /// <summary>
    /// Metadata dictionary for handlers to share information.
    /// </summary>
    public Dictionary<string, object> Metadata { get; } = new();
    
    /// <summary>
    /// The time when the pipeline execution started.
    /// </summary>
    public DateTime StartTime { get; }
    
    /// <summary>
    /// Unique identifier for this translation request.
    /// </summary>
    public Guid RequestId { get; }
    
    public TranslationContext(ChatMessage message, string sourceLanguage = "auto", string targetLanguage = "en")
    {
        OriginalMessage = message ?? throw new ArgumentNullException(nameof(message));
        SourceLanguage = sourceLanguage;
        TargetLanguage = targetLanguage;
        StartTime = DateTime.UtcNow;
        RequestId = Guid.NewGuid();
    }
    
    /// <summary>
    /// Calculates the elapsed time since pipeline start.
    /// </summary>
    public TimeSpan GetElapsedTime() => DateTime.UtcNow - StartTime;
    
    /// <summary>
    /// Sets the result and marks the context as handled.
    /// </summary>
    public void SetResult(TranslationResult result)
    {
        Result = result;
        IsHandled = true;
    }
}

/// <summary>
/// Registry interface for managing pipeline handlers.
/// </summary>
public interface IPipelineHandlerRegistry
{
    /// <summary>
    /// Registers a new pipeline handler.
    /// </summary>
    void RegisterHandler(ITranslationPipelineHandler handler);
    
    /// <summary>
    /// Unregisters a pipeline handler by name.
    /// </summary>
    bool UnregisterHandler(string handlerName);
    
    /// <summary>
    /// Gets all registered handlers.
    /// </summary>
    IReadOnlyList<ITranslationPipelineHandler> GetHandlers();
    
    /// <summary>
    /// Gets a specific handler by name.
    /// </summary>
    ITranslationPipelineHandler? GetHandler(string handlerName);
}
