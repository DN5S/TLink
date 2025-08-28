using System;
using TLink.Core.MVU;
using TLink.Modules.Chat.Models;

namespace TLink.Modules.Translation.Models;

public abstract record TranslationAction : IAction
{
    public string Type => GetType().Name;
    public DateTime Timestamp { get; } = DateTime.UtcNow;
}

// Request translation of a message
public record TranslateRequestAction(ChatMessage Message) : TranslationAction;

// Provider changed
public record ProviderChangedAction(string ProviderName, bool SupportsXml) : TranslationAction;

// Translation lifecycle
public record TranslationStartedAction(TranslationRequest Request) : TranslationAction;
public record TranslationCompletedAction(TranslationRequest Request, TranslationResult Result) : TranslationAction;
public record TranslationFailedAction(TranslationRequest Request, string Error) : TranslationAction;

// Cache management
public record ClearCacheAction : TranslationAction;
public record UpdateCacheSettingsAction(bool EnableCache, int CacheSize) : TranslationAction;