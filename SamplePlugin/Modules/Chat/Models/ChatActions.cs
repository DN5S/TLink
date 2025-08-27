using System;
using Dalamud.Game.Text;
using SamplePlugin.Core.MVU;

namespace SamplePlugin.Modules.Chat.Models;

public abstract record ChatAction(DateTime Timestamp) : IAction
{
    public abstract string Type { get; }
}

public record AddMessageAction(ChatMessage Message) : ChatAction(DateTime.Now)
{
    public override string Type => "Chat/AddMessage";
}

public record SetFilterAction(string FilterText) : ChatAction(DateTime.Now)
{
    public override string Type => "Chat/SetFilter";
}

public record ToggleChannelAction(XivChatType Channel) : ChatAction(DateTime.Now)
{
    public override string Type => "Chat/ToggleChannel";
}

public record ClearMessagesAction() : ChatAction(DateTime.Now)
{
    public override string Type => "Chat/ClearMessages";
}

public record UpdateMaxMessagesAction(int MaxMessages) : ChatAction(DateTime.Now)
{
    public override string Type => "Chat/UpdateMaxMessages";
}

public record UpdateAutoScrollAction(bool AutoScroll) : ChatAction(DateTime.Now)
{
    public override string Type => "Chat/UpdateAutoScroll";
}

public record UpdateShowTimestampsAction(bool ShowTimestamps) : ChatAction(DateTime.Now)
{
    public override string Type => "Chat/UpdateShowTimestamps";
}

public record ResetChannelFiltersAction() : ChatAction(DateTime.Now)
{
    public override string Type => "Chat/ResetChannelFilters";
}

public record LoadConfigurationAction(ChatModuleConfiguration Configuration) : ChatAction(DateTime.Now)
{
    public override string Type => "Chat/LoadConfiguration";
}
