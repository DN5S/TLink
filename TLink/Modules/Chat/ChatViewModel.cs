using System;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using ModuleKit.MVU;
using TLink.Modules.Chat.Models;

namespace TLink.Modules.Chat;

public class ChatViewModel : IDisposable
{
    private Store<ChatState>? store;
    private ChatState? currentState;
    private IDisposable? storeSubscription;
    
    public void Initialize(Store<ChatState> chatStore)
    {
        store = chatStore;
        storeSubscription = store.StateChanged.Subscribe(state => currentState = state);
    }
    
    public void DrawUI()
    {
        // Debug UI to monitor chat messages
        if (currentState == null || store == null) return;
        
        if (ImGui.Begin("Chat Module Debug", ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text($"Module Status: {(currentState.IsEnabled ? "Active" : "Inactive")}");
            ImGui.Text($"Enabled Channels: {currentState.EnabledChannels.Count}");
            ImGui.Text($"Messages Captured: {currentState.RecentMessages.Count}");
            
            ImGui.Separator();
            
            // Test button to simulate messages
            if (ImGui.Button("Simulate Test Message"))
            {
                var testMessage = new ChatMessage
                {
                    Type = XivChatType.Say,
                    Timestamp = DateTime.Now,
                    Sender = "Debug Tester",
                    Message = $"Test message at {DateTime.Now:HH:mm:ss}",
                    SeStringSender = new SeString(),
                    SeStringMessage = new SeString()
                };
                store.Dispatch(new MessageReceivedAction(testMessage));
            }
            
            ImGui.Separator();
            ImGui.Text("Recent Messages:");
            
            if (ImGui.BeginChild("MessageList", new System.Numerics.Vector2(500, 200), true))
            {
                foreach (var msg in currentState.RecentMessages.TakeLast(20).Reverse())
                {
                    ImGui.Text($"[{msg.Timestamp:HH:mm:ss}] {GetChannelDisplayName(msg.Type)}: {msg.Sender}: {msg.Message}");
                }
            }
            ImGui.EndChild();
        }
        ImGui.End();
    }
    
    public void DrawConfiguration()
    {
        if (currentState == null || store == null) return;
        
        ImGui.Text("Chat Module Configuration");
        ImGui.Separator();
        
        var isEnabled = currentState.IsEnabled;
        if (ImGui.Checkbox("Enable Chat Monitoring", ref isEnabled))
        {
            store.Dispatch(new SetEnabledAction(isEnabled));
        }
        
        ImGui.Spacing();
        ImGui.Text("Monitored Chat Channels:");
        
        // Group channels by type for a better organization
        DrawChannelGroup("Public", [
            XivChatType.Say,
            XivChatType.Yell,
            XivChatType.Shout
        ]);
        
        DrawChannelGroup("Party", [
            XivChatType.Party,
            XivChatType.Alliance
        ]);
        
        DrawChannelGroup("Free Company", [
            XivChatType.FreeCompany
        ]);
        
        DrawChannelGroup("Linkshells", [
            XivChatType.Ls1, XivChatType.Ls2, XivChatType.Ls3, XivChatType.Ls4,
            XivChatType.Ls5, XivChatType.Ls6, XivChatType.Ls7, XivChatType.Ls8
        ]);
        
        DrawChannelGroup("Cross-world Linkshells", [
            XivChatType.CrossLinkShell1, XivChatType.CrossLinkShell2,
            XivChatType.CrossLinkShell3, XivChatType.CrossLinkShell4,
            XivChatType.CrossLinkShell5, XivChatType.CrossLinkShell6,
            XivChatType.CrossLinkShell7, XivChatType.CrossLinkShell8
        ]);
        
        ImGui.Spacing();
        ImGui.Separator();
        
        var maxHistory = currentState.MaxMessageHistory;
        if (ImGui.InputInt("Max Message History", ref maxHistory))
        {
            if (maxHistory is > 0 and <= 1000)
            {
                store.Dispatch(new SetMaxHistoryAction(maxHistory));
            }
        }
        
        if (ImGui.Button("Clear Message History"))
        {
            store.Dispatch(new ClearHistoryAction());
        }
        
        ImGui.Spacing();
        ImGui.Text($"Messages in history: {currentState.RecentMessages.Count}");
    }
    
    private void DrawChannelGroup(string groupName, XivChatType[] channels)
    {
        if (currentState == null || store == null) return;
        
        if (ImGui.TreeNode(groupName))
        {
            foreach (var channel in channels)
            {
                var isEnabled = currentState.EnabledChannels.Contains(channel);
                var channelName = GetChannelDisplayName(channel);
                
                if (ImGui.Checkbox(channelName, ref isEnabled))
                {
                    store.Dispatch(new ToggleChannelAction(channel));
                }
            }
            ImGui.TreePop();
        }
    }
    
    private static string GetChannelDisplayName(XivChatType type)
    {
        return type switch
        {
            XivChatType.Say => "Say",
            XivChatType.Yell => "Yell",
            XivChatType.Shout => "Shout",
            XivChatType.Party => "Party",
            XivChatType.Alliance => "Alliance",
            XivChatType.FreeCompany => "Free Company",
            XivChatType.Ls1 => "LS-1",
            XivChatType.Ls2 => "LS-2",
            XivChatType.Ls3 => "LS-3",
            XivChatType.Ls4 => "LS-4",
            XivChatType.Ls5 => "LS-5",
            XivChatType.Ls6 => "LS-6",
            XivChatType.Ls7 => "LS-7",
            XivChatType.Ls8 => "LS-8",
            XivChatType.CrossLinkShell1 => "CWLS-1",
            XivChatType.CrossLinkShell2 => "CWLS-2",
            XivChatType.CrossLinkShell3 => "CWLS-3",
            XivChatType.CrossLinkShell4 => "CWLS-4",
            XivChatType.CrossLinkShell5 => "CWLS-5",
            XivChatType.CrossLinkShell6 => "CWLS-6",
            XivChatType.CrossLinkShell7 => "CWLS-7",
            XivChatType.CrossLinkShell8 => "CWLS-8",
            _ => type.ToString()
        };
    }
    
    public void Dispose()
    {
        storeSubscription?.Dispose();
        GC.SuppressFinalize(this);
    }
}
