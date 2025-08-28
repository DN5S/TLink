using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using TLink.Core.MVU;
using TLink.Modules.Translation.Configuration;
using TLink.Modules.Translation.Models;
using TLink.Modules.Translation.Services;

namespace TLink.Modules.Translation.UI;

public class TranslationViewModel : IDisposable
{
    private Store<TranslationState>? store;
    private IDisposable? stateSubscription;
    private TranslationConfig? config;
    
    // Observable properties for UI binding
    public ObservableCollection<string> AvailableProviders { get; } = [];
    public ObservableCollection<TranslationHistoryItem> RecentTranslations { get; } = [];

    // Statistics
    public int TotalTranslations { get; private set; }
    public int CacheHits { get; private set; }
    public int FailedTranslations { get; private set; }
    public double CacheHitRate => TotalTranslations > 0 ? (double)CacheHits / TotalTranslations : 0;
    public double AverageTranslationTime { get; private set; }
    
    // Provider info
    public string ActiveProvider { get; private set; } = string.Empty;
    public bool ProviderSupportsFormatting { get; private set; }
    public bool IsTranslating { get; private set; }
    
    // Configuration
    public string[] SupportedLanguages { get; } =
    [
        "auto", "en", "ja", "ko", "zh", "fr", "de", "es", "ru", "pt", "it", "nl", "pl"
    ];

    public void Initialize(
        Store<TranslationState> store, 
        TranslationConfig config,
        Dictionary<string, ITranslationProvider> providers)
    {
        this.store = store;
        this.config = config;
        
        // Initialize provider list
        UpdateProviderList(providers);
        
        // Subscribe to state changes
        stateSubscription = store.StateChanged
            .Subscribe(state =>
            {
                // Update statistics
                TotalTranslations = state.Statistics.TotalTranslations;
                CacheHits = state.Statistics.CacheHits;
                FailedTranslations = state.Statistics.FailedTranslations;
                AverageTranslationTime = state.Statistics.AverageTranslationTime;
                
                // Update provider info
                ActiveProvider = state.ActiveProvider;
                ProviderSupportsFormatting = state.ProviderSupportsXml;
                IsTranslating = state.IsTranslating;
                
                // Update translation history from pending/completed
                UpdateTranslationHistory(state);
            });
    }
    
    public void UpdateProviderList(Dictionary<string, ITranslationProvider> providers)
    {
        AvailableProviders.Clear();
        foreach (var providerName in providers.Keys.OrderBy(k => k))
        {
            AvailableProviders.Add(providerName);
        }
    }
    
    public void UpdateCacheSettings(bool enable, int size)
    {
        // Dispatch action to update cache settings in state
        // Config is already updated by the Window
        store?.Dispatch(new UpdateCacheSettingsAction(enable, size));
    }
    
    public void ClearCache()
    {
        store?.Dispatch(new ClearCacheAction());
    }
    
    private void UpdateTranslationHistory(TranslationState state)
    {
        // Keep only the last 50 translations for display
        while (RecentTranslations.Count > 50)
        {
            RecentTranslations.RemoveAt(0);
        }
        
        // Add any new completed translations
        // This is simplified - in a real implementation you'd track which ones are new
    }
    
    public void Dispose()
    {
        stateSubscription?.Dispose();
        GC.SuppressFinalize(this);
    }
}

public class TranslationHistoryItem
{
    public DateTime Timestamp { get; init; }
    public string Channel { get; init; } = string.Empty;
    public string Sender { get; init; } = string.Empty;
    public string OriginalText { get; init; } = string.Empty;
    public string TranslatedText { get; init; } = string.Empty;
    public bool FormattingPreserved { get; init; }
    public TimeSpan TranslationTime { get; init; }
}
