using System;
using System.Collections.Generic;
using Dalamud.Game.Text;
using TLink.Core.Configuration;

namespace TLink.Modules.Chat;

[Serializable]
public class ChatModuleConfiguration : ModuleConfiguration
{
    public int MaxMessages { get; set; } = 1000;
    public bool ShowTimestamps { get; set; } = true;
    public bool AutoScroll { get; set; } = true;
    public HashSet<XivChatType> EnabledChannels { get; set; }
    
    public ChatModuleConfiguration()
    {
        ModuleName = "Chat";
        EnabledChannels = GetDefaultChannels();
    }
    
    public static HashSet<XivChatType> GetDefaultChannels()
    {
        return
        [
            XivChatType.Say,
            XivChatType.Shout,
            XivChatType.TellIncoming,
            XivChatType.TellOutgoing,
            XivChatType.Party,
            XivChatType.Alliance,
            XivChatType.FreeCompany,
            XivChatType.Ls1,
            XivChatType.CrossLinkShell1,
            XivChatType.NPCDialogue,
            XivChatType.NPCDialogueAnnouncements
        ];
    }
    
    public void ResetChannels()
    {
        EnabledChannels = GetDefaultChannels();
    }
}
