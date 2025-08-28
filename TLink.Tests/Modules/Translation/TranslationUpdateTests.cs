using System;
using System.Collections.Immutable;
using FluentAssertions;
using TLink.Core.MVU;
using TLink.Modules.Chat.Models;
using TLink.Modules.Translation.Models;
using TLink.Modules.Translation.MVU;
using Xunit;

namespace TLink.Tests.Modules.Translation;

public class TranslationUpdateTests
{
    [Fact]
    public void HandleTranslateRequest_WhenNoActiveProvider_ReturnsNoChange()
    {
        // Arrange
        var state = TranslationState.Initial;
        var message = CreateTestChatMessage("Hello");
        var action = new TranslateRequestAction(message);
        
        // Act
        var result = TranslationUpdate.Update(state, action);
        
        // Assert
        result.NewState.Should().Be(state);
        result.Effects.Should().BeEmpty();
    }
    
    [Fact]
    public void HandleTranslateRequest_WhenCacheHit_ReturnsPublishCachedEffect()
    {
        // Arrange
        var message = CreateTestChatMessage("Hello");
        var cacheKey = "Say:Hello";
        var cachedResult = new TranslationResult("Hello", "안녕하세요", false, TimeSpan.FromMilliseconds(100));
        
        var state = TranslationState.Initial with
        {
            ActiveProvider = "TestProvider",
            TranslationCache = ImmutableDictionary<string, TranslationResult>.Empty.Add(cacheKey, cachedResult)
        };
        
        var action = new TranslateRequestAction(message);
        
        // Act
        var result = TranslationUpdate.Update(state, action);
        
        // Assert
        result.NewState.Statistics.CacheHits.Should().Be(1);
        result.Effects.Should().HaveCount(1);
        result.Effects[0].Should().BeOfType<PublishCachedTranslationEffect>();
    }
    
    [Fact]
    public void HandleTranslateRequest_WhenCacheMiss_AddsToQueueAndReturnsRouteEffect()
    {
        // Arrange
        var message = CreateTestChatMessage("Hello");
        var state = TranslationState.Initial with
        {
            ActiveProvider = "TestProvider"
        };
        
        var action = new TranslateRequestAction(message);
        
        // Act
        var result = TranslationUpdate.Update(state, action);
        
        // Assert
        result.NewState.PendingTranslations.Should().HaveCount(1);
        result.NewState.PendingTranslations[0].Message.Should().Be(message);
        result.Effects.Should().HaveCount(1);
        result.Effects[0].Should().BeOfType<RouteToProviderEffect>();
    }
    
    [Fact]
    public void HandleProviderChanged_UpdatesProviderInfo()
    {
        // Arrange
        var state = TranslationState.Initial;
        var action = new ProviderChangedAction("DeepL", true);
        
        // Act
        var result = TranslationUpdate.Update(state, action);
        
        // Assert
        result.NewState.ActiveProvider.Should().Be("DeepL");
        result.NewState.ProviderSupportsXml.Should().BeTrue();
        result.Effects.Should().BeEmpty();
    }
    
    [Fact]
    public void HandleTranslationCompleted_RemovesFromPendingAndUpdatesCache()
    {
        // Arrange
        var message = CreateTestChatMessage("Hello");
        var request = new TranslationRequest(
            Guid.NewGuid(),
            message,
            "auto",
            "ko",
            DateTime.UtcNow
        );
        
        var state = TranslationState.Initial with
        {
            PendingTranslations = ImmutableList<TranslationRequest>.Empty.Add(request),
            IsTranslating = true
        };
        
        var translationResult = new TranslationResult(
            "Hello",
            "안녕하세요",
            false,
            TimeSpan.FromMilliseconds(150)
        );
        
        var action = new TranslationCompletedAction(request, translationResult);
        
        // Act
        var result = TranslationUpdate.Update(state, action);
        
        // Assert
        result.NewState.PendingTranslations.Should().BeEmpty();
        result.NewState.TranslationCache.Should().ContainKey("Say:Hello");
        result.NewState.Statistics.TotalTranslations.Should().Be(1);
        result.NewState.Statistics.AverageTranslationTime.Should().Be(150);
        result.NewState.IsTranslating.Should().BeFalse();
        result.Effects.Should().BeEmpty();
    }
    
    [Fact]
    public void HandleTranslationFailed_RemovesFromPendingAndIncrementsFailures()
    {
        // Arrange
        var message = CreateTestChatMessage("Hello");
        var request = new TranslationRequest(
            Guid.NewGuid(),
            message,
            "auto",
            "ko",
            DateTime.UtcNow
        );
        
        var state = TranslationState.Initial with
        {
            PendingTranslations = ImmutableList<TranslationRequest>.Empty.Add(request),
            IsTranslating = true
        };
        
        var action = new TranslationFailedAction(request, "Network error");
        
        // Act
        var result = TranslationUpdate.Update(state, action);
        
        // Assert
        result.NewState.PendingTranslations.Should().BeEmpty();
        result.NewState.Statistics.FailedTranslations.Should().Be(1);
        result.NewState.IsTranslating.Should().BeFalse();
        result.Effects.Should().BeEmpty();
    }
    
    [Fact]
    public void HandleClearCache_EmptiesTranslationCache()
    {
        // Arrange
        var state = TranslationState.Initial with
        {
            TranslationCache = ImmutableDictionary<string, TranslationResult>.Empty
                .Add("key1", new TranslationResult("text1", "trans1", false, TimeSpan.Zero))
                .Add("key2", new TranslationResult("text2", "trans2", false, TimeSpan.Zero))
        };
        
        var action = new ClearCacheAction();
        
        // Act
        var result = TranslationUpdate.Update(state, action);
        
        // Assert
        result.NewState.TranslationCache.Should().BeEmpty();
        result.Effects.Should().BeEmpty();
    }
    
    [Fact]
    public void HandleUpdateCacheSettings_WhenDisabled_ClearsCache()
    {
        // Arrange
        var state = TranslationState.Initial with
        {
            TranslationCache = ImmutableDictionary<string, TranslationResult>.Empty
                .Add("key1", new TranslationResult("text1", "trans1", false, TimeSpan.Zero))
        };
        
        var action = new UpdateCacheSettingsAction(false, 100);
        
        // Act
        var result = TranslationUpdate.Update(state, action);
        
        // Assert
        result.NewState.TranslationCache.Should().BeEmpty();
        result.Effects.Should().BeEmpty();
    }
    
    private static ChatMessage CreateTestChatMessage(string text)
    {
        return new ChatMessage
        {
            Type = Dalamud.Game.Text.XivChatType.Say,
            Timestamp = DateTime.Now,
            Sender = "TestUser",
            Message = text,
            SeStringSender = new Dalamud.Game.Text.SeStringHandling.SeString(),
            SeStringMessage = new Dalamud.Game.Text.SeStringHandling.SeString()
        };
    }
}