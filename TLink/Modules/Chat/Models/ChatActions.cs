using System;
using System.Collections.Generic;
using Dalamud.Game.Text;
using TLink.Core.MVU;

namespace TLink.Modules.Chat.Models;

public abstract record ChatAction(DateTime Timestamp) : IAction
{
    public abstract string Type { get; }
}

public record ToggleTranslatableChannelAction(XivChatType Channel) : ChatAction(DateTime.Now)
{
    public override string Type => "Chat/ToggleTranslatableChannel";
}

public record SetTranslatableChannelsAction(HashSet<XivChatType> Channels) : ChatAction(DateTime.Now)
{
    public override string Type => "Chat/SetTranslatableChannels";
}

public record ResetTranslatableChannelsAction() : ChatAction(DateTime.Now)
{
    public override string Type => "Chat/ResetTranslatableChannels";
}

public record LoadConfigurationAction(ChatModuleConfiguration Configuration) : ChatAction(DateTime.Now)
{
    public override string Type => "Chat/LoadConfiguration";
}
