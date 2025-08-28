using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using TLink.Core.MVU;
using TLink.Modules.Translation.Models;

namespace TLink.Modules.Translation.UI;

/// <summary>
/// ViewModel for the Translation orchestrator UI.
/// Displays pipeline statistics and handler information.
/// </summary>
public class TranslationViewModel : IDisposable
{
    private Store<TranslationState>? store;
    private IDisposable? stateSubscription;
    
    // Observable properties for UI binding
    public ObservableCollection<PipelineHandlerInfo> RegisteredHandlers { get; } = [];
    public ObservableCollection<PipelineExecutionInfo> ActiveExecutions { get; } = [];
    public List<string> AllSupportedLanguages { get; private set; } = [];
    
    // Pipeline Statistics
    public int TotalExecutions { get; private set; }
    public int SuccessfulExecutions { get; private set; }
    public int FailedExecutions { get; private set; }
    public double SuccessRate => TotalExecutions > 0 ? (double)SuccessfulExecutions / TotalExecutions : 0;
    public double AveragePipelineTime { get; private set; }
    
    // Pipeline State
    public bool IsProcessing { get; private set; }
    public int HandlerCount => RegisteredHandlers.Count;
    public int EnabledHandlerCount => RegisteredHandlers.Count(h => h.IsEnabled);
    
    public void Initialize(Store<TranslationState> translationStore)
    {
        store = translationStore;
        
        // Subscribe to state changes
        stateSubscription = store.StateChanged.Subscribe(UpdateFromState);
        
        // Initial update
        UpdateFromState(store.State);
    }
    
    private void UpdateFromState(TranslationState state)
    {
        // Update handler collection
        RegisteredHandlers.Clear();
        foreach (var handler in state.RegisteredHandlers)
        {
            RegisteredHandlers.Add(handler);
        }
        
        // Update active executions
        ActiveExecutions.Clear();
        foreach (var execution in state.ActiveExecutions.Values)
        {
            ActiveExecutions.Add(new PipelineExecutionInfo(
                execution.RequestId,
                execution.Context.OriginalMessage.Message,
                execution.StartTime,
                execution.ExecutedHandlers.Count
            ));
        }
        
        // Update statistics
        TotalExecutions = state.Statistics.TotalExecutions;
        SuccessfulExecutions = state.Statistics.SuccessfulExecutions;
        FailedExecutions = state.Statistics.FailedExecutions;
        AveragePipelineTime = state.Statistics.AveragePipelineTime;
        
        // Update processing state
        IsProcessing = state.IsProcessing;
        
        // Aggregate supported languages from all enabled handlers
        AllSupportedLanguages = state.RegisteredHandlers
            .Where(h => h.IsEnabled)
            .SelectMany(h => h.SupportedLanguages)
            .Distinct()
            .OrderBy(lang => lang == "auto" ? 0 : 1)  // "auto" first
            .ThenBy(lang => lang)
            .ToList();
    }
    
    public void EnableHandler(string handlerName, bool isEnabled)
    {
        store?.Dispatch(new EnableHandlerAction(handlerName, isEnabled));
    }
    
    public void ResetStatistics()
    {
        store?.Dispatch(new ResetStatisticsAction());
    }
    
    public void Dispose()
    {
        stateSubscription?.Dispose();
        RegisteredHandlers.Clear();
        ActiveExecutions.Clear();
        GC.SuppressFinalize(this);
    }
}

// Helper class for displaying execution info in UI
public record PipelineExecutionInfo(
    Guid RequestId,
    string Message,
    DateTime StartTime,
    int HandlersExecuted
);
