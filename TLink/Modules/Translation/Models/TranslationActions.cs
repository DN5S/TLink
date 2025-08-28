using System;
using TLink.Core.MVU;
using TLink.Modules.Chat.Models;
using TLink.Modules.Translation.Services;

namespace TLink.Modules.Translation.Models;

public abstract record TranslationAction : IAction
{
    public string Type => GetType().Name;
    public DateTime Timestamp { get; } = DateTime.UtcNow;
}

// Pipeline Handler Management
public record RegisterHandlerAction(
    ITranslationPipelineHandler Handler,
    string ModuleName
) : TranslationAction;

public record UnregisterHandlerAction(
    string HandlerName
) : TranslationAction;

public record EnableHandlerAction(
    string HandlerName,
    bool IsEnabled
) : TranslationAction;

// Pipeline Execution
public record ExecutePipelineAction(
    ChatMessage Message,
    string SourceLanguage = "auto",
    string TargetLanguage = "en"
) : TranslationAction;

public record PipelineStartedAction(
    Guid RequestId,
    TranslationContext Context
) : TranslationAction;

public record PipelineHandlerExecutedAction(
    Guid RequestId,
    string HandlerName,
    TimeSpan ExecutionTime,
    bool TerminatedPipeline
) : TranslationAction;

public record PipelineCompletedAction(
    Guid RequestId,
    TranslationResult? Result,
    TimeSpan TotalExecutionTime
) : TranslationAction;

public record PipelineFailedAction(
    Guid RequestId,
    string Error,
    string FailedHandlerName
) : TranslationAction;

// Statistics
public record ResetStatisticsAction : TranslationAction;