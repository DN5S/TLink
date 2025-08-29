using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Interface.Utility.Raii;
using TLink.Core.MVU;
using TLink.Modules.Chat.Models;

namespace TLink.Modules.Chat;

public class ChatViewModel : IDisposable
{
    private IStore<ChatState>? store;
    private IDisposable? stateSubscription;
    
    public void Initialize(IStore<ChatState> chatStore)
    {
        store = chatStore;
        stateSubscription = store.StateChanged.Subscribe(_ => { /* State change tracking if needed */ });
    }
    
    public void DrawConfigurationUI()
    {
        if (store == null) return;
        
        ImGui.Text("Select channels to translate:");
        ImGui.Spacing();
        
        // Group channels by category for better organization
        DrawChannelCategory("Combat", XivChatType.Say, XivChatType.Yell, XivChatType.Shout);
        DrawChannelCategory("Party", XivChatType.Party, XivChatType.Alliance);
        DrawChannelCategory("Social", XivChatType.FreeCompany, XivChatType.Ls1, XivChatType.Ls2, XivChatType.Ls3, XivChatType.Ls4, XivChatType.Ls5, XivChatType.Ls6, XivChatType.Ls7, XivChatType.Ls8);
        DrawChannelCategory("Cross-world", XivChatType.CrossLinkShell1, XivChatType.CrossLinkShell2, XivChatType.CrossLinkShell3, XivChatType.CrossLinkShell4, XivChatType.CrossLinkShell5, XivChatType.CrossLinkShell6, XivChatType.CrossLinkShell7, XivChatType.CrossLinkShell8);
        DrawChannelCategory("Private", XivChatType.TellIncoming, XivChatType.TellOutgoing);
        DrawChannelCategory("NPC", XivChatType.NPCDialogue, XivChatType.NPCDialogueAnnouncements);
        
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        
        if (ImGui.Button("Reset to Defaults"))
        {
            store.Dispatch(new ResetTranslatableChannelsAction());
        }
    }
    
    private void DrawChannelCategory(string categoryName, params XivChatType[] channels)
    {
        if (store == null) return;

        using var node = ImRaii.TreeNode(categoryName);
        if (node.Success)
        {
            foreach (var channel in channels)
            {
                var isEnabled = store.State.TranslatableChannels.Contains(channel);
                if (ImGui.Checkbox($"##{categoryName}_{channel}", ref isEnabled))
                {
                    store.Dispatch(new ToggleTranslatableChannelAction(channel));
                }
                ImGui.SameLine();
                ImGui.Text(GetChannelDisplayName(channel));
            }
        }
    }
    
    private static string GetChannelDisplayName(XivChatType channel)
    {
        return channel switch
        {
            XivChatType.Say => "Say",
            XivChatType.Yell => "Yell",
            XivChatType.Shout => "Shout",
            XivChatType.Party => "Party",
            XivChatType.Alliance => "Alliance",
            XivChatType.FreeCompany => "Free Company",
            XivChatType.TellIncoming => "Tell (Incoming)",
            XivChatType.TellOutgoing => "Tell (Outgoing)",
            XivChatType.NPCDialogue => "NPC Dialogue",
            XivChatType.NPCDialogueAnnouncements => "NPC Announcements",
            >= XivChatType.Ls1 and <= XivChatType.Ls8 => 
                $"LS-{(int)channel - (int)XivChatType.Ls1 + 1}",
            >= XivChatType.CrossLinkShell1 and <= XivChatType.CrossLinkShell8 => 
                $"CWLS-{(int)channel - (int)XivChatType.CrossLinkShell1 + 1}",
            _ => channel.ToString()
        };
    }
    
    public void Dispose()
    {
        stateSubscription?.Dispose();
        GC.SuppressFinalize(this);
    }
}
