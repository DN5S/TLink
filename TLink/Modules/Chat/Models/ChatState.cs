using System;
using System.Collections.Immutable;
using Dalamud.Game.Text;
using TLink.Core.MVU;

namespace TLink.Modules.Chat.Models;

public record ChatState : IState
{
    public string Id { get; init; } = "ChatState";
    public long Version { get; init; }
    
    public ImmutableHashSet<XivChatType> TranslatableChannels { get; init; } = ImmutableHashSet<XivChatType>.Empty;
    
    object ICloneable.Clone() => this with { };
    
    public static ChatState Initial => new()
    {
        TranslatableChannels = ChatModuleConfiguration.GetDefaultTranslatableChannels().ToImmutableHashSet()
    };
}