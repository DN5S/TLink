using System;
using System.Collections.Immutable;
using TLink.Core.MVU;
using TLink.Modules.Chat.Models;

namespace TLink.Modules.Translation.Models;

public record TranslationState : IState
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public long Version { get; init; } = 0;
    
    public ImmutableList<TranslationRequest> PendingTranslations { get; init; } = ImmutableList<TranslationRequest>.Empty;
    public ImmutableDictionary<string, TranslationResult> TranslationCache { get; init; } = ImmutableDictionary<string, TranslationResult>.Empty;
    public string ActiveProvider { get; init; } = string.Empty;
    public bool ProviderSupportsXml { get; init; }
    public bool IsTranslating { get; init; }
    public TranslationStatistics Statistics { get; init; } = new();
    
    public static TranslationState Initial => new();
    
    object ICloneable.Clone() => this with { };
}

public record TranslationRequest(
    Guid Id,
    ChatMessage Message,
    string SourceLanguage,
    string TargetLanguage,
    DateTime RequestTime
);

public record TranslationResult(
    string OriginalText,
    string TranslatedText,
    bool FormattingPreserved,
    TimeSpan TranslationTime
);

public record TranslationStatistics(
    int TotalTranslations = 0,
    int CacheHits = 0,
    int FailedTranslations = 0,
    double AverageTranslationTime = 0
);