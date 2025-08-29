using System;
using System.Collections.Generic;
using Dalamud.Game.Text;
using TLink.Core.Configuration;

namespace TLink.Modules.Chat;

[Serializable]
public class ChatModuleConfiguration : ModuleConfiguration
{
    public HashSet<XivChatType> TranslatableChannels { get; set; }
    
    public ChatModuleConfiguration()
    {
        ModuleName = "Chat";
        TranslatableChannels = GetDefaultTranslatableChannels();
    }
    
    public static HashSet<XivChatType> GetDefaultTranslatableChannels()
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
        TranslatableChannels = GetDefaultTranslatableChannels();
    }
}
