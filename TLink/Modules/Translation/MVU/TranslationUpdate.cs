using System;
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
            TranslateRequestAction req => HandleTranslateRequest(state, req),
            ProviderChangedAction changed => HandleProviderChanged(state, changed),
            TranslationStartedAction started => HandleTranslationStarted(state, started),
            TranslationCompletedAction completed => HandleTranslationCompleted(state, completed),
            TranslationFailedAction failed => HandleTranslationFailed(state, failed),
            ClearCacheAction => HandleClearCache(state),
            UpdateCacheSettingsAction settings => HandleUpdateCacheSettings(state, settings),
            _ => UpdateResult<TranslationState>.NoChange(state)
        };
    }
    
    private static UpdateResult<TranslationState> HandleTranslateRequest(
        TranslationState state, 
        TranslateRequestAction action)
    {
        // Check if a provider is available
        if (string.IsNullOrEmpty(state.ActiveProvider))
        {
            return UpdateResult<TranslationState>.NoChange(state);
        }
        
        // Check cache first
        var cacheKey = GenerateCacheKey(action.Message);
        if (state.TranslationCache.TryGetValue(cacheKey, out var cached))
        {
            // Update statistics for cache hit
            var stats = state.Statistics with
            {
                CacheHits = state.Statistics.CacheHits + 1
            };
            
            return UpdateResult<TranslationState>.WithEffects(
                state with { Statistics = stats },
                new PublishCachedTranslationEffect(action.Message, cached)
            );
        }
        
        // Create translation request
        var request = new TranslationRequest(
            Guid.NewGuid(),
            action.Message,
            "auto", // TODO: Get from config
            "en",   // TODO: Get from config
            DateTime.UtcNow
        );
        
        var newState = state with
        {
            PendingTranslations = state.PendingTranslations.Add(request)
        };
        
        // Create an effect to route to provider
        return UpdateResult<TranslationState>.WithEffects(
            newState,
            new RouteToProviderEffect(request)
        );
    }
    
    private static UpdateResult<TranslationState> HandleProviderChanged(
        TranslationState state,
        ProviderChangedAction action)
    {
        return UpdateResult<TranslationState>.StateOnly(state with
        {
            ActiveProvider = action.ProviderName,
            ProviderSupportsXml = action.SupportsXml
        });
    }
    
    private static UpdateResult<TranslationState> HandleTranslationStarted(
        TranslationState state,
        TranslationStartedAction action)
    {
        return UpdateResult<TranslationState>.StateOnly(state with 
        { 
            IsTranslating = true 
        });
    }
    
    private static UpdateResult<TranslationState> HandleTranslationCompleted(
        TranslationState state,
        TranslationCompletedAction action)
    {
        // Remove from pending
        var newPending = state.PendingTranslations
            .RemoveAll(r => r.Id == action.Request.Id);
        
        // Add to cache
        var cacheKey = GenerateCacheKey(action.Request.Message);
        var newCache = state.TranslationCache;
        
        // Simple cache eviction - remove arbitrary item when full
        // TODO: Implement proper LRU cache with timestamp tracking
        if (state.TranslationCache.Count >= 100) // TODO: Use config value
        {
            // WARNING: This removes an arbitrary item, not the oldest
            // For true LRU, we'd need to track access times
            var keyToRemove = newCache.Keys.First();
            newCache = newCache.Remove(keyToRemove);
        }
        
        newCache = newCache.Add(cacheKey, action.Result);
        
        // Update statistics
        var stats = state.Statistics with
        {
            TotalTranslations = state.Statistics.TotalTranslations + 1,
            AverageTranslationTime = CalculateNewAverage(
                state.Statistics.AverageTranslationTime,
                state.Statistics.TotalTranslations,
                action.Result.TranslationTime.TotalMilliseconds
            )
        };
        
        return UpdateResult<TranslationState>.StateOnly(state with
        {
            PendingTranslations = newPending,
            TranslationCache = newCache,
            IsTranslating = newPending.Any(),
            Statistics = stats
        });
    }
    
    private static UpdateResult<TranslationState> HandleTranslationFailed(
        TranslationState state,
        TranslationFailedAction action)
    {
        // Remove from pending
        var newPending = state.PendingTranslations
            .RemoveAll(r => r.Id == action.Request.Id);
        
        // Update statistics
        var stats = state.Statistics with
        {
            FailedTranslations = state.Statistics.FailedTranslations + 1
        };
        
        return UpdateResult<TranslationState>.StateOnly(state with
        {
            PendingTranslations = newPending,
            IsTranslating = newPending.Any(),
            Statistics = stats
        });
    }
    
    private static UpdateResult<TranslationState> HandleClearCache(TranslationState state)
    {
        return UpdateResult<TranslationState>.StateOnly(state with
        {
            TranslationCache = ImmutableDictionary<string, TranslationResult>.Empty
        });
    }
    
    private static UpdateResult<TranslationState> HandleUpdateCacheSettings(
        TranslationState state,
        UpdateCacheSettingsAction action)
    {
        // If the cache is disabled, clear it
        if (!action.EnableCache)
        {
            return UpdateResult<TranslationState>.StateOnly(state with
            {
                TranslationCache = ImmutableDictionary<string, TranslationResult>.Empty
            });
        }
        
        // If cache size reduced, trim cache
        // WARNING: This keeps arbitrary items, not necessarily the most recent
        if (action.CacheSize < state.TranslationCache.Count)
        {
            var newCache = state.TranslationCache
                .Take(action.CacheSize)
                .ToImmutableDictionary();
            
            return UpdateResult<TranslationState>.StateOnly(state with
            {
                TranslationCache = newCache
            });
        }
        
        return UpdateResult<TranslationState>.NoChange(state);
    }
    
    private static string GenerateCacheKey(Chat.Models.ChatMessage message)
    {
        // Simple cache key based on message text
        // Could be enhanced to include language settings
        return $"{message.Type}:{message.Message}";
    }
    
    private static double CalculateNewAverage(double currentAvg, int count, double newValue)
    {
        if (count == 0) return newValue;
        return ((currentAvg * count) + newValue) / (count + 1);
    }
}
