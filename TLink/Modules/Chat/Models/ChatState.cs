using System;
using System.Collections.Immutable;
using Dalamud.Game.Text;
using ModuleKit.MVU;

namespace TLink.Modules.Chat.Models;

public record ChatState : IState
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public long Version { get; init; }
    public ImmutableList<ChatMessage> RecentMessages { get; init; } = ImmutableList<ChatMessage>.Empty;
    public ImmutableHashSet<XivChatType> EnabledChannels { get; init; } = ImmutableHashSet<XivChatType>.Empty;
    public int MaxMessageHistory { get; init; } = 100;
    public bool IsEnabled { get; init; } = true;
    
    public static ChatState Initial => new() 
    { 
        Id = "chat-state",
        Version = 0,
        EnabledChannels = ImmutableHashSet.Create(
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
        )
    };
    
    object ICloneable.Clone() => this with { };
}
