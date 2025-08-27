using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility.Raii;
using SamplePlugin.Core.UI;
using Dalamud.Bindings.ImGui;

namespace SamplePlugin.Modules.ChatAnalyzer;

public class ChatAnalyzerWindow : Window, IDisposable
{
    private readonly ChatAnalyzerViewModel viewModel;
    private readonly ChatAnalyzerModuleConfiguration configuration;
    private readonly Action saveConfiguration;
    
    public ChatAnalyzerWindow(ChatAnalyzerViewModel viewModel, ChatAnalyzerModuleConfiguration configuration, Action saveConfiguration) 
        : base("Chat Analyzer###ChatAnalyzer", ImGuiWindowFlags.None)
    {
        this.viewModel = viewModel;
        this.configuration = configuration;
        this.saveConfiguration = saveConfiguration;
        
        Size = new Vector2(400, 500);
        SizeCondition = ImGuiCond.FirstUseEver;
    }
    
    public override void Draw()
    {
        ImGui.Text("Chat Statistics Analyzer");
        ImGui.TextDisabled("Analyzes patterns from the Chat module");
        ImGui.Separator();
        
        // Summary section
        if (LayoutHelpers.BeginSection("Summary"))
        {
            ImGui.Text($"Total Messages Analyzed: {viewModel.TotalMessages}");
            ImGui.Text($"Average Message Length: {viewModel.AverageMessageLength:F1} characters");
            LayoutHelpers.EndSection();
        }
        
        ImGui.Spacing();
        
        // Detailed statistics
        if (LayoutHelpers.BeginSection("Detailed Statistics"))
        {
            {
                using var table = ImRaii.Table("StatsTable", 2, 
                                               ImGuiTableFlags.BordersV | 
                                               ImGuiTableFlags.RowBg |
                                               ImGuiTableFlags.Resizable);
                
                if (table)
                {
                    ImGui.TableSetupColumn("Metric", ImGuiTableColumnFlags.WidthFixed, 150);
                    ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableHeadersRow();
                    
                    foreach (var stat in viewModel.Statistics)
                    {
                        ImGui.TableNextRow();
                        
                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(stat.Name);
                        
                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(stat.Value);
                    }
                }
            }
            LayoutHelpers.EndSection();
        }
        
        ImGui.Spacing();
        ImGui.Separator();
        
        // Control buttons
        if (ImGui.Button("Reset Statistics"))
        {
            viewModel.Reset();
        }
        
        ImGui.SameLine();
        LayoutHelpers.HelpTooltip("Clear all collected statistics and start fresh");
    }
    
    public void DrawConfiguration()
    {
        ImGui.Text("Chat Analyzer Configuration");
        ImGui.Separator();
        
        ImGui.TextWrapped("This module analyzes chat messages received by the Chat module. " +
                          "It provides statistics on message patterns, sender activity, and word frequency.");
        
        ImGui.Spacing();
        
        var analysisInterval = configuration.AnalysisInterval;
        if (ImGui.InputInt("Analysis Interval (seconds)", ref analysisInterval, 10, 60))
        {
            configuration.AnalysisInterval = Math.Clamp(analysisInterval, 0, 3600);
            saveConfiguration();
        }
        LayoutHelpers.HelpTooltip("How often to analyze messages (0 = analyze every message)");
        
        var trackPatterns = configuration.TrackPatterns;
        if (ImGui.Checkbox("Track Word Patterns", ref trackPatterns))
        {
            configuration.TrackPatterns = trackPatterns;
            saveConfiguration();
        }
        LayoutHelpers.HelpTooltip("Analyze word frequency patterns in messages");
        
        var trackSenderStats = configuration.TrackSenderStatistics;
        if (ImGui.Checkbox("Track Sender Statistics", ref trackSenderStats))
        {
            configuration.TrackSenderStatistics = trackSenderStats;
            saveConfiguration();
        }
        LayoutHelpers.HelpTooltip("Track statistics for message senders");
        
        var showRealTimeUpdates = configuration.ShowRealTimeUpdates;
        if (ImGui.Checkbox("Show Real-time Updates", ref showRealTimeUpdates))
        {
            configuration.ShowRealTimeUpdates = showRealTimeUpdates;
            saveConfiguration();
        }
        LayoutHelpers.HelpTooltip("Update statistics in real-time (may impact performance)");
        
        ImGui.Spacing();
        
        var maxStats = configuration.MaxStatisticsCount;
        if (ImGui.InputInt("Max Statistics Count", ref maxStats, 10, 50))
        {
            configuration.MaxStatisticsCount = Math.Clamp(maxStats, 10, 1000);
            saveConfiguration();
        }
        LayoutHelpers.HelpTooltip("Maximum number of statistics to keep");
    }
    
    public void Dispose()
    {
        // Cleanup if needed
        GC.SuppressFinalize(this);
    }
}
