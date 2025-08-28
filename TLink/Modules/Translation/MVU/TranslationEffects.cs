using System;
using System.Collections.Immutable;
using TLink.Core.MVU;
using TLink.Modules.Chat.Models;
using TLink.Modules.Translation.Models;

namespace TLink.Modules.Translation.MVU;

public abstract record TranslationEffect : IEffect
{
    public string Type => GetType().Name;
}

// Pipeline Execution Effects
public record ExecutePipelineEffect(
    ChatMessage Message,
    string SourceLanguage,
    string TargetLanguage
) : TranslationEffect;

// Event Publishing Effects
public record PublishHandlerRegisteredEffect(
    PipelineHandlerInfo Handler
) : TranslationEffect;

public record PublishHandlerUnregisteredEffect(
    PipelineHandlerInfo Handler
) : TranslationEffect;

public record PublishPipelineStartedEffect(
    Guid RequestId,
    ChatMessage Message
) : TranslationEffect;

public record PublishPipelineCompletedEffect(
    Guid RequestId,
    TranslationResult? Result,
    TimeSpan ExecutionTime,
    ImmutableList<string> ExecutedHandlers
) : TranslationEffect;

public record PublishTranslatedMessageEffect(
    ChatMessage OriginalMessage,
    TranslationResult Result
) : TranslationEffect;

public record PublishTranslationErrorEffect(
    ChatMessage OriginalMessage,
    string Error
) : TranslationEffect;
