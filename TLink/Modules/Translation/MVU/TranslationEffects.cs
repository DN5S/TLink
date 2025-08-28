using TLink.Core.MVU;
using TLink.Modules.Chat.Models;
using TLink.Modules.Translation.Models;

namespace TLink.Modules.Translation.MVU;

public abstract record TranslationEffect : IEffect
{
    public string Type => GetType().Name;
}

// Route translation request to active provider
public record RouteToProviderEffect(TranslationRequest Request) : TranslationEffect;

// Publish a cached translation result
public record PublishCachedTranslationEffect(
    ChatMessage OriginalMessage,
    TranslationResult CachedResult
) : TranslationEffect;
