using System;
using System.Collections.Generic;
using TLink.Modules.Chat.Models;

namespace TLink.Modules.Translation.Models;

// --- Final Result Events (for external modules) ---

// Published when translation is successfully completed
// Can be published by any handler in the pipeline that completes the translation
public record MessageTranslatedEvent(
    ChatMessage OriginalMessage,
    TranslationResult TranslatedResult
);

// Published when a translation pipeline fails
public record TranslationErrorEvent(
    ChatMessage OriginalMessage,
    string Error
);

// --- Pipeline Observation Events (for debugging and UI) ---

// Published when a handler is registered to the pipeline
public record PipelineHandlerRegistered(
    PipelineHandlerInfo Handler
);

// Published when a handler is unregistered from the pipeline
public record PipelineHandlerUnregistered(
    PipelineHandlerInfo Handler
);

// Published when pipeline execution starts
public record PipelineExecutionStarted(
    Guid RequestId,
    ChatMessage Message
);

// Published when pipeline execution completes
public record PipelineExecutionCompleted(
    Guid RequestId,
    TranslationResult? Result,
    TimeSpan ExecutionTime,
    IReadOnlyList<string> ExecutedHandlers,
    string? TerminatingHandler // Which handler -> terminated the pipeline
);
