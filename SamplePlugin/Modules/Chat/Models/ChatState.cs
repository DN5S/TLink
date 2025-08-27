using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Dalamud.Game.Text;
using SamplePlugin.Core.MVU;

namespace SamplePlugin.Modules.Chat.Models;

public record ChatState : IState
{
    public string Id { get; init; } = "ChatState";
    public long Version { get; init; }
    
    public ImmutableList<ChatMessage> Messages { get; init; } = ImmutableList<ChatMessage>.Empty;
    public ImmutableHashSet<XivChatType> EnabledChannels { get; init; } = ImmutableHashSet<XivChatType>.Empty;
    public string Filter { get; init; } = string.Empty;
    public int MaxMessages { get; init; } = 1000;
    public bool AutoScroll { get; init; } = true;
    public bool ShowTimestamps { get; init; } = true;
    
    object ICloneable.Clone() => this with { };
    
    public static ChatState Initial => new()
    {
        EnabledChannels = ImmutableHashSet.Create(
            XivChatType.Say,
            XivChatType.Shout,
            XivChatType.Party,
            XivChatType.Alliance,
            XivChatType.FreeCompany
        )
    };
    
    public IEnumerable<ChatMessage> FilteredMessages
    {
        get
        {
            if (string.IsNullOrEmpty(Filter))
            {
                foreach (var msg in Messages)
                {
                    if (EnabledChannels.Contains(msg.Type))
                        yield return msg;
                }
            }
            else
            {
                foreach (var msg in Messages)
                {
                    if (EnabledChannels.Contains(msg.Type) &&
                        (msg.Message.Contains(Filter, StringComparison.OrdinalIgnoreCase) ||
                         msg.Sender.Contains(Filter, StringComparison.OrdinalIgnoreCase)))
                    {
                        yield return msg;
                    }
                }
            }
        }
    }
}