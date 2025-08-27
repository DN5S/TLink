using System;
using System.Linq;
using FluentAssertions;
using SamplePlugin.Modules.Chat.Models;
using SamplePlugin.Modules.ChatAnalyzer;
using Xunit;
using Dalamud.Game.Text;

namespace SamplePlugin.Tests.Modules.ChatAnalyzer;

public class ChatAnalyzerViewModelTests : IDisposable
{
    private readonly ChatAnalyzerViewModel viewModel;
    private readonly ChatAnalyzerModuleConfiguration configuration;

    public ChatAnalyzerViewModelTests()
    {
        viewModel = new ChatAnalyzerViewModel();
        configuration = new ChatAnalyzerModuleConfiguration
        {
            ModuleName = "ChatAnalyzer",
            IsEnabled = true,
            TrackPatterns = true,
            TrackSenderStatistics = true,
            ShowRealTimeUpdates = true,
            AnalysisInterval = 0,
            MaxStatisticsCount = 100
        };
        viewModel.Initialize(configuration);
    }

    [Fact]
    public void AnalyzeMessage_ShouldIncrementTotalMessages()
    {
        // Arrange
        var message = CreateTestMessage("Hello world", "Player1");
        
        // Act
        viewModel.AnalyzeMessage(message);
        
        // Assert
        viewModel.TotalMessages.Should().Be(1);
    }
    
    [Fact]
    public void AnalyzeMessage_MultipleTimes_ShouldAccumulateStatistics()
    {
        // Arrange
        var messages = new[]
        {
            CreateTestMessage("Hello world", "Player1"),
            CreateTestMessage("How are you?", "Player2"),
            CreateTestMessage("I'm fine, thanks!", "Player1")
        };
        
        // Act
        foreach (var message in messages)
        {
            viewModel.AnalyzeMessage(message);
        }
        
        // Assert
        viewModel.TotalMessages.Should().Be(3);
        viewModel.Statistics.Should().NotBeEmpty();
    }
    
    [Fact]
    public void AnalyzeMessage_ShouldIdentifyMostActiveSender()
    {
        // Arrange & Act
        viewModel.AnalyzeMessage(CreateTestMessage("Message 1", "Alice"));
        viewModel.AnalyzeMessage(CreateTestMessage("Message 2", "Bob"));
        viewModel.AnalyzeMessage(CreateTestMessage("Message 3", "Alice"));
        viewModel.AnalyzeMessage(CreateTestMessage("Message 4", "Alice"));
        
        // Assert
        viewModel.MostActiveSender.Should().Be("Alice");
    }
    
    [Fact]
    public void AnalyzeMessage_ShouldCalculateAverageMessageLength()
    {
        // Arrange & Act
        viewModel.AnalyzeMessage(CreateTestMessage("Short", "Player"));     // 5 chars
        viewModel.AnalyzeMessage(CreateTestMessage("Medium msg", "Player")); // 10 chars
        viewModel.AnalyzeMessage(CreateTestMessage("This is longer", "Player")); // 14 chars
        
        // Assert
        viewModel.AverageMessageLength.Should().BeApproximately(9.67, 0.01);
    }
    
    [Fact]
    public void AnalyzeMessage_ShouldIdentifyMostCommonWord()
    {
        // Arrange & Act
        viewModel.AnalyzeMessage(CreateTestMessage("Hello world hello", "Player1"));
        viewModel.AnalyzeMessage(CreateTestMessage("World of warcraft", "Player2"));
        viewModel.AnalyzeMessage(CreateTestMessage("Hello again world", "Player3"));
        
        // Assert
        // "hello" and "world" both appear 3 times, but only words > 3 chars are counted
        viewModel.MostCommonWord.Should().BeOneOf("hello", "world");
    }
    
    [Fact]
    public void AnalyzeMessage_ShouldIgnoreShortWords()
    {
        // Arrange & Act
        viewModel.AnalyzeMessage(CreateTestMessage("The cat is on the mat", "Player"));
        viewModel.AnalyzeMessage(CreateTestMessage("A big cat", "Player"));
        
        // Assert
        // Words <= 3 characters should be ignored
        var stats = viewModel.Statistics.FirstOrDefault(s => s.Name == "Most Common Word");
        stats?.Value.Should().NotContain("the");
        stats?.Value.Should().NotContain("is");
        stats?.Value.Should().NotContain("on");
    }
    
    [Fact]
    public void Statistics_ShouldContainExpectedEntries()
    {
        // Arrange & Act
        viewModel.AnalyzeMessage(CreateTestMessage("Test message", "Player1"));
        viewModel.AnalyzeMessage(CreateTestMessage("Another test", "Player2"));
        
        // Assert
        var statNames = viewModel.Statistics.Select(s => s.Name).ToList();
        statNames.Should().Contain("Total Messages");
        statNames.Should().Contain("Most Active Sender");
        statNames.Should().Contain("Average Message Length");
        statNames.Should().Contain("Most Common Word");
        statNames.Should().Contain("Unique Senders");
    }
    
    [Fact]
    public void Statistics_ShouldShowTopSenders()
    {
        // Arrange & Act
        for (var i = 0; i < 10; i++)
        {
            viewModel.AnalyzeMessage(CreateTestMessage($"Message {i}", $"Player{i % 3}"));
        }
        
        // Assert
        var topSenderStats = viewModel.Statistics
            .Where(s => s.Name.StartsWith("Top Sender #"))
            .ToList();
        
        topSenderStats.Should().NotBeEmpty();
        topSenderStats.Should().HaveCountLessThanOrEqualTo(5);
    }
    
    [Fact]
    public void Reset_ShouldClearAllStatistics()
    {
        // Arrange
        viewModel.AnalyzeMessage(CreateTestMessage("Test message", "Player"));
        viewModel.AnalyzeMessage(CreateTestMessage("Another message", "Player"));
        
        // Act
        viewModel.Reset();
        
        // Assert
        viewModel.TotalMessages.Should().Be(0);
        viewModel.AverageMessageLength.Should().Be(0);
        viewModel.MostActiveSender.Should().Be("N/A");
        viewModel.MostCommonWord.Should().Be("N/A");
        viewModel.Statistics.Should().BeEmpty();
    }
    
    [Fact]
    public void AnalyzeMessage_WithEmptySender_ShouldStillCountMessage()
    {
        // Arrange
        var message = new ChatMessage
        {
            Type = XivChatType.SystemMessage,
            Sender = "",
            Message = "System message",
            Timestamp = DateTime.Now
        };
        
        // Act
        viewModel.AnalyzeMessage(message);
        
        // Assert
        viewModel.TotalMessages.Should().Be(1);
    }
    
    [Fact]
    public void AnalyzeMessage_CaseInsensitiveWordCounting()
    {
        // Arrange & Act
        viewModel.AnalyzeMessage(CreateTestMessage("Hello WORLD", "Player1"));
        viewModel.AnalyzeMessage(CreateTestMessage("hello World", "Player2"));
        viewModel.AnalyzeMessage(CreateTestMessage("HELLO world", "Player3"));
        
        // Assert
        // All variations of "hello" and "world" should be counted together
        var stats = viewModel.Statistics.FirstOrDefault(s => s.Name == "Most Common Word");
        stats?.Value.Should().Contain("(3x)");
    }
    
    [Fact]
    public void Statistics_UniqueSendersCount()
    {
        // Arrange & Act
        viewModel.AnalyzeMessage(CreateTestMessage("Message", "Alice"));
        viewModel.AnalyzeMessage(CreateTestMessage("Message", "Bob"));
        viewModel.AnalyzeMessage(CreateTestMessage("Message", "Charlie"));
        viewModel.AnalyzeMessage(CreateTestMessage("Message", "Alice")); // Duplicate sender
        
        // Assert
        var uniqueSendersStat = viewModel.Statistics
            .FirstOrDefault(s => s.Name == "Unique Senders");
        uniqueSendersStat?.Value.Should().Be("3");
    }
    
    [Fact]
    public void Dispose_ShouldCleanupResources()
    {
        // Act & Assert - Should not throw
        viewModel.Dispose();
    }
    
    public void Dispose()
    {
        viewModel?.Dispose();
    }
    
    // Helper method to create test messages
    private static ChatMessage CreateTestMessage(string content, string sender)
    {
        return new ChatMessage
        {
            Type = XivChatType.Say,
            Sender = sender,
            Message = content,
            Timestamp = DateTime.Now
        };
    }
}
