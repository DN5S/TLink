using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Dalamud.Game.Text.SeStringHandling;
using TLink.Core.MVU;
using TLink.Modules.Translation.Services;

namespace TLink.Modules.Translation.Models;

public record TranslationState : IState
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public long Version { get; init; } = 0;
    
    // Pipeline Orchestration State
    public ImmutableList<PipelineHandlerInfo> RegisteredHandlers { get; init; } = ImmutableList<PipelineHandlerInfo>.Empty;
    public ImmutableDictionary<Guid, PipelineExecution> ActiveExecutions { get; init; } = ImmutableDictionary<Guid, PipelineExecution>.Empty;
    public PipelineStatistics Statistics { get; init; } = new();
    
    // Computed Properties
    public bool IsProcessing => !ActiveExecutions.IsEmpty;
    
    public static TranslationState Initial => new();
    
    object ICloneable.Clone() => this with { };
}

// Supporting Records for Pipeline Architecture

public record PipelineHandlerInfo(
    string Name,
    int Priority,
    string ModuleName,
    bool IsEnabled,
    DateTime RegisteredAt,
    IReadOnlyList<string> SupportedLanguages,
    int ExecutionCount = 0,
    string? LastError = null
);

public record PipelineExecution(
    Guid RequestId,
    TranslationContext Context,
    DateTime StartTime,
    ImmutableList<string> ExecutedHandlers
);

public record PipelineStatistics(
    int TotalExecutions = 0,
    int SuccessfulExecutions = 0,
    int FailedExecutions = 0,
    double AveragePipelineTime = 0,
    ImmutableDictionary<string, HandlerStatistics>? HandlerStats = null
)
{
    public PipelineStatistics() : this(0, 0, 0, 0, ImmutableDictionary<string, HandlerStatistics>.Empty) { }
}

public record HandlerStatistics(
    int ExecutionCount = 0,
    int TerminationCount = 0,
    double AverageExecutionTime = 0
);


public record TranslationResult(
    string TranslatedText,
    SeString TranslatedSeString,
    string? DetectedLanguage,
    string TranslatedBy,
    bool FromCache,
    TimeSpan TranslationTime
);
