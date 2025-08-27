using System;
using System.Collections.Generic;
using Dalamud.Game.Text;
using SamplePlugin.Core.Configuration;

namespace SamplePlugin.Modules.Chat;

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
            XivChatType.Ls2,
            XivChatType.Ls3,
            XivChatType.Ls4,
            XivChatType.Ls5,
            XivChatType.Ls6,
            XivChatType.Ls7,
            XivChatType.Ls8
        ];
    }
    
    public void ResetChannels()
    {
        EnabledChannels = GetDefaultChannels();
    }
}
