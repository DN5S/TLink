using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Subjects;
using SamplePlugin.Modules.Chat.Models;

namespace SamplePlugin.Modules.ChatAnalyzer;

public class ChatAnalyzerViewModel : IDisposable
{
    private readonly Dictionary<string, int> wordFrequency = new();
    private readonly Dictionary<string, int> senderMessageCount = new();
    private readonly Subject<ChatStatistic> statisticUpdated = new();
    private ChatAnalyzerModuleConfiguration? configuration;
    
    public ObservableCollection<ChatStatistic> Statistics { get; } = [];

    public IObservable<ChatStatistic> StatisticUpdated => statisticUpdated;
    
    public int TotalMessages { get; private set; }
    public string MostActiveSender { get; private set; } = "N/A";
    public string MostCommonWord { get; private set; } = "N/A";
    public double AverageMessageLength { get; private set; }
    
    private double totalMessageLength;
    private DateTime lastAnalysisTime = DateTime.Now;
    
    public void Initialize(ChatAnalyzerModuleConfiguration config)
    {
        configuration = config;
    }
    
    public void AnalyzeMessage(ChatMessage message)
    {
        if (configuration == null) return;
        
        // Check if we should perform analysis based on an interval
        if (configuration.AnalysisInterval > 0)
        {
            var timeSinceLastAnalysis = (DateTime.Now - lastAnalysisTime).TotalSeconds;
            if (timeSinceLastAnalysis < configuration.AnalysisInterval && !configuration.ShowRealTimeUpdates)
            {
                return; // Skip analysis if an interval hasn't passed
            }
            lastAnalysisTime = DateTime.Now;
        }
        
        // Update total count
        TotalMessages++;
        
        // Update sender statistics if enabled
        if (configuration.TrackSenderStatistics && !string.IsNullOrEmpty(message.Sender))
        {
            senderMessageCount.TryAdd(message.Sender, 0);
            senderMessageCount[message.Sender]++;
            
            MostActiveSender = senderMessageCount
                .OrderByDescending(kvp => kvp.Value)
                .FirstOrDefault().Key ?? "N/A";
        }
        
        // Update message length statistics
        totalMessageLength += message.Message.Length;
        AverageMessageLength = totalMessageLength / TotalMessages;
        
        // Analyze words if pattern tracking is enabled
        if (configuration.TrackPatterns)
        {
            var words = message.Message
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 3); // Only count words longer than 3 characters
            
            foreach (var word in words)
            {
                var lowerWord = word.ToLowerInvariant();
                wordFrequency.TryAdd(lowerWord, 0);
                wordFrequency[lowerWord]++;
            }
            
            if (wordFrequency.Count != 0)
            {
                MostCommonWord = wordFrequency
                    .OrderByDescending(kvp => kvp.Value)
                    .FirstOrDefault().Key ?? "N/A";
            }
        }
        
        UpdateStatistics();
    }
    
    private void UpdateStatistics()
    {
        Statistics.Clear();
        
        // Limit statistics count based on configuration
        var maxStats = configuration?.MaxStatisticsCount ?? 100;
        
        Statistics.Add(new ChatStatistic
        {
            Name = "Total Messages",
            Value = TotalMessages.ToString()
        });
        
        Statistics.Add(new ChatStatistic
        {
            Name = "Most Active Sender",
            Value = $"{MostActiveSender} ({senderMessageCount.GetValueOrDefault(MostActiveSender, 0)} msgs)"
        });
        
        Statistics.Add(new ChatStatistic
        {
            Name = "Average Message Length",
            Value = $"{AverageMessageLength:F1} characters"
        });
        
        Statistics.Add(new ChatStatistic
        {
            Name = "Most Common Word",
            Value = $"{MostCommonWord} ({wordFrequency.GetValueOrDefault(MostCommonWord, 0)}x)"
        });
        
        Statistics.Add(new ChatStatistic
        {
            Name = "Unique Senders",
            Value = senderMessageCount.Count.ToString()
        });
        
        // Add the top 5 most active senders
        var topSenders = senderMessageCount
            .OrderByDescending(kvp => kvp.Value)
            .Take(5)
            .ToList();
        
        for (var i = 0; i < topSenders.Count; i++)
        {
            Statistics.Add(new ChatStatistic
            {
                Name = $"Top Sender #{i + 1}",
                Value = $"{topSenders[i].Key} ({topSenders[i].Value} msgs)"
            });
        }
        
        // Limit statistics to configure maximum
        while (Statistics.Count > maxStats)
        {
            Statistics.RemoveAt(Statistics.Count - 1);
        }
    }
    
    public void Reset()
    {
        TotalMessages = 0;
        totalMessageLength = 0;
        AverageMessageLength = 0;
        MostActiveSender = "N/A";
        MostCommonWord = "N/A";
        
        wordFrequency.Clear();
        senderMessageCount.Clear();
        Statistics.Clear();
    }
    
    public void Dispose()
    {
        statisticUpdated.Dispose();
        GC.SuppressFinalize(this);
    }
}

public record ChatStatistic
{
    public string Name { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
}
