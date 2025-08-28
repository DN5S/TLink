using System;
using TLink.Core.Configuration;

namespace TLink.Modules.ChatAnalyzer;

[Serializable]
public class ChatAnalyzerModuleConfiguration : ModuleConfiguration
{
    public int AnalysisInterval { get; set; } = 60; // seconds
    public bool TrackPatterns { get; set; } = true;
    public bool TrackSenderStatistics { get; set; } = true;
    public int MaxStatisticsCount { get; set; } = 100;
    public bool ShowRealTimeUpdates { get; set; }
    
    public ChatAnalyzerModuleConfiguration()
    {
        ModuleName = "ChatAnalyzer";
    }
}
