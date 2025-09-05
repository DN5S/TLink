using System;
using System.Collections.Generic;
using Dalamud.Game.Text;
using ModuleKit.MVU;

namespace TLink.Modules.Chat.Models;

public record MessageReceivedAction(ChatMessage Message) : IAction
{
    public string Type => "Chat/MessageReceived";
    public DateTime Timestamp { get; } = DateTime.UtcNow;
}

public record LoadConfigurationAction(ChatModuleConfiguration Configuration) : IAction
{
    public string Type => "Chat/LoadConfiguration";
    public DateTime Timestamp { get; } = DateTime.UtcNow;
}

public record ToggleChannelAction(XivChatType Channel) : IAction
{
    public string Type => "Chat/ToggleChannel";
    public DateTime Timestamp { get; } = DateTime.UtcNow;
}

public record SetEnabledChannelsAction(IEnumerable<XivChatType> Channels) : IAction
{
    public string Type => "Chat/SetEnabledChannels";
    public DateTime Timestamp { get; } = DateTime.UtcNow;
}

public record SetMaxHistoryAction(int MaxHistory) : IAction
{
    public string Type => "Chat/SetMaxHistory";
    public DateTime Timestamp { get; } = DateTime.UtcNow;
}

public record ClearHistoryAction : IAction
{
    public string Type => "Chat/ClearHistory";
    public DateTime Timestamp { get; } = DateTime.UtcNow;
}

public record SetEnabledAction(bool IsEnabled) : IAction
{
    public string Type => "Chat/SetEnabled";
    public DateTime Timestamp { get; } = DateTime.UtcNow;
}