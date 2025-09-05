using System.Collections.Generic;
using Dalamud.Game.Text;
using ModuleKit.Configuration;

namespace TLink.Modules.Chat;

public class ChatModuleConfiguration : ModuleConfiguration
{
    public HashSet<XivChatType> EnabledChannels { get; set; } =
    [
        XivChatType.Say,
        XivChatType.Yell,
        XivChatType.Shout,
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
        XivChatType.Ls8,
        XivChatType.CrossLinkShell1,
        XivChatType.CrossLinkShell2,
        XivChatType.CrossLinkShell3,
        XivChatType.CrossLinkShell4,
        XivChatType.CrossLinkShell5,
        XivChatType.CrossLinkShell6,
        XivChatType.CrossLinkShell7,
        XivChatType.CrossLinkShell8
    ];
    
    public int MaxMessageHistory { get; set; } = 100;
}
