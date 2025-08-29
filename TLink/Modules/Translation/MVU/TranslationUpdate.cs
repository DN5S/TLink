using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using TLink.Core.MVU;
using TLink.Modules.Translation.Models;

namespace TLink.Modules.Translation.MVU;

public static class TranslationUpdate
{
    public static UpdateResult<TranslationState> Update(TranslationState state, IAction action)
    {
        return action switch
        {
            // Handler Management
            RegisterHandlerAction register => HandleRegisterHandler(state, register),
            UnregisterHandlerAction unregister => HandleUnregisterHandler(state, unregister),
            EnableHandlerAction enable => HandleEnableHandler(state, enable),
            
            // Pipeline Execution Lifecycle
            ExecutePipelineAction execute => HandleExecutePipeline(state, execute),
            PipelineStartedAction started => HandlePipelineStarted(state, started),
            PipelineHandlerExecutedAction executed => HandleHandlerExecuted(state, executed),
            PipelineCompletedAction completed => HandlePipelineCompleted(state, completed),
            PipelineFailedAction failed => HandlePipelineFailed(state, failed),
            
            // Statistics
            ResetStatisticsAction => HandleResetStatistics(state),
            
            _ => UpdateResult<TranslationState>.NoChange(state)
        };
    }
    
    // --- Handler Management ---
    
    private static UpdateResult<TranslationState> HandleRegisterHandler(
        TranslationState state,
        RegisterHandlerAction action)
    {
        // Prevent duplicate registration
        if (state.RegisteredHandlers.Any(h => h.Name == action.Handler.Name))
        {
            return UpdateResult<TranslationState>.NoChange(state);
        }
        
        var handlerInfo = new PipelineHandlerInfo(
            action.Handler.Name,
            action.Handler.Priority,
            action.ModuleName,
            action.Handler.IsEnabled,
            DateTime.UtcNow,
            action.Handler.SupportedLanguages
        );
        
        // Add and sort by priority
        var newHandlers = state.RegisteredHandlers
            .Add(handlerInfo)
            .Sort((a, b) => a.Priority.CompareTo(b.Priority));
        
        return UpdateResult<TranslationState>.WithEffects(
            state with { RegisteredHandlers = newHandlers },
            new PublishHandlerRegisteredEffect(handlerInfo)
        );
    }
    
    private static UpdateResult<TranslationState> HandleUnregisterHandler(
        TranslationState state,
        UnregisterHandlerAction action)
    {
        var handler = state.RegisteredHandlers.FirstOrDefault(h => h.Name == action.HandlerName);
        if (handler == null)
        {
            return UpdateResult<TranslationState>.NoChange(state);
        }
        
        var newHandlers = state.RegisteredHandlers.Remove(handler);
        
        return UpdateResult<TranslationState>.WithEffects(
            state with { RegisteredHandlers = newHandlers },
            new PublishHandlerUnregisteredEffect(handler)
        );
    }
    
    private static UpdateResult<TranslationState> HandleEnableHandler(
        TranslationState state,
        EnableHandlerAction action)
    {
        var handlerIndex = state.RegisteredHandlers.FindIndex(h => h.Name == action.HandlerName);
        if (handlerIndex < 0)
        {
            return UpdateResult<TranslationState>.NoChange(state);
        }
        
        var handler = state.RegisteredHandlers[handlerIndex];
        var updatedHandler = handler with { IsEnabled = action.IsEnabled };
        var newHandlers = state.RegisteredHandlers.SetItem(handlerIndex, updatedHandler);
        
        return UpdateResult<TranslationState>.StateOnly(
            state with { RegisteredHandlers = newHandlers }
        );
    }
    
    // --- Pipeline Execution Lifecycle ---
    
    private static UpdateResult<TranslationState> HandleExecutePipeline(
        TranslationState state,
        ExecutePipelineAction action)
    {
        // Always create the effect - the effect handler will check for enabled handlers
        // This avoids timing issues with async handler registration
        return UpdateResult<TranslationState>.WithEffects(
            state,
            new ExecutePipelineEffect(action.Message, action.SourceLanguage, action.TargetLanguage)
        );
    }
    
    private static UpdateResult<TranslationState> HandlePipelineStarted(
        TranslationState state,
        PipelineStartedAction action)
    {
        var execution = new PipelineExecution(
            action.RequestId,
            action.Context,
            DateTime.UtcNow,
            ImmutableList<string>.Empty
        );
        
        var newExecutions = state.ActiveExecutions.Add(action.RequestId, execution);
        
        return UpdateResult<TranslationState>.WithEffects(
            state with { ActiveExecutions = newExecutions },
            new PublishPipelineStartedEffect(action.RequestId, action.Context.OriginalMessage)
        );
    }
    
    private static UpdateResult<TranslationState> HandleHandlerExecuted(
        TranslationState state,
        PipelineHandlerExecutedAction action)
    {
        if (!state.ActiveExecutions.TryGetValue(action.RequestId, out var execution))
        {
            return UpdateResult<TranslationState>.NoChange(state);
        }
        
        // Update execution with the handler name
        var updatedExecution = execution with
        {
            ExecutedHandlers = execution.ExecutedHandlers.Add(action.HandlerName)
        };
        
        var newExecutions = state.ActiveExecutions.SetItem(action.RequestId, updatedExecution);
        
        // Update handler statistics
        var handlerStats = state.Statistics.HandlerStats?.GetValueOrDefault(
            action.HandlerName,
            new HandlerStatistics()
        ) ?? new HandlerStatistics();

        var updatedHandlerStats = new HandlerStatistics(ExecutionCount:
                                                        handlerStats.ExecutionCount + 1, TerminationCount:
                                                        action.TerminatedPipeline
                                                            ? handlerStats.TerminationCount + 1
                                                            : handlerStats.TerminationCount,
                                                        AverageExecutionTime: CalculateNewAverage(
                                                            handlerStats.AverageExecutionTime,
                                                            handlerStats.ExecutionCount,
                                                            action.ExecutionTime.TotalMilliseconds
                                                        ));
        
        var newHandlerStats = (state.Statistics.HandlerStats ?? ImmutableDictionary<string, HandlerStatistics>.Empty)
            .SetItem(action.HandlerName, updatedHandlerStats);
        
        var newStatistics = state.Statistics with
        {
            HandlerStats = newHandlerStats
        };
        
        return UpdateResult<TranslationState>.StateOnly(state with
        {
            ActiveExecutions = newExecutions,
            Statistics = newStatistics
        });
    }
    
    private static UpdateResult<TranslationState> HandlePipelineCompleted(
        TranslationState state,
        PipelineCompletedAction action)
    {
        if (!state.ActiveExecutions.TryGetValue(action.RequestId, out var execution))
        {
            return UpdateResult<TranslationState>.NoChange(state);
        }
        
        var newExecutions = state.ActiveExecutions.Remove(action.RequestId);
        
        // Update pipeline statistics
        var newStatistics = state.Statistics with
        {
            TotalExecutions = state.Statistics.TotalExecutions + 1,
            SuccessfulExecutions = action.Result != null
                ? state.Statistics.SuccessfulExecutions + 1
                : state.Statistics.SuccessfulExecutions,
            AveragePipelineTime = CalculateNewAverage(
                state.Statistics.AveragePipelineTime,
                state.Statistics.TotalExecutions,
                action.TotalExecutionTime.TotalMilliseconds
            )
        };
        
        var effects = new List<IEffect>
        {
            // Publish pipeline completion event
            new PublishPipelineCompletedEffect(
                action.RequestId,
                action.Result,
                action.TotalExecutionTime,
                execution.ExecutedHandlers
            )
        };

        // If translation successful, publish the translated message event
        if (action.Result != null)
        {
            effects.Add(new PublishTranslatedMessageEffect(
                execution.Context.OriginalMessage,
                action.Result
            ));
        }
        
        return UpdateResult<TranslationState>.WithEffects(
            state with
            {
                ActiveExecutions = newExecutions,
                Statistics = newStatistics
            },
            effects.ToArray()
        );
    }
    
    private static UpdateResult<TranslationState> HandlePipelineFailed(
        TranslationState state,
        PipelineFailedAction action)
    {
        if (!state.ActiveExecutions.TryGetValue(action.RequestId, out var execution))
        {
            return UpdateResult<TranslationState>.NoChange(state);
        }
        
        var newExecutions = state.ActiveExecutions.Remove(action.RequestId);
        
        // Update pipeline statistics
        var newStatistics = state.Statistics with
        {
            TotalExecutions = state.Statistics.TotalExecutions + 1,
            FailedExecutions = state.Statistics.FailedExecutions + 1
        };
        
        return UpdateResult<TranslationState>.WithEffects(
            state with
            {
                ActiveExecutions = newExecutions,
                Statistics = newStatistics
            },
            new PublishTranslationErrorEffect(
                execution.Context.OriginalMessage,
                action.Error
            )
        );
    }
    
    private static UpdateResult<TranslationState> HandleResetStatistics(TranslationState state)
    {
        return UpdateResult<TranslationState>.StateOnly(state with
        {
            Statistics = new PipelineStatistics()
        });
    }
    
    // --- Helper Methods ---
    
    private static double CalculateNewAverage(double currentAvg, int count, double newValue)
    {
        if (count == 0) return newValue;
        return ((currentAvg * count) + newValue) / (count + 1);
    }
}
