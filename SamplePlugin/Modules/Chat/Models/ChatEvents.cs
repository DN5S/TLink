using System.Collections.Generic;
using Dalamud.Game.Text;

namespace SamplePlugin.Modules.Chat.Models;

public record ChatMessageReceived(ChatMessage Message);
public record ChatFilterChanged(string Filter);
public record ChatChannelToggled(string Channel, bool IsEnabled);

// Configuration change events - for unidirectional data flow
public abstract record ConfigurationChange;
public record MaxMessagesChangeRequested(int MaxMessages) : ConfigurationChange;
public record AutoScrollChangeRequested(bool AutoScroll) : ConfigurationChange;
public record ShowTimestampsChangeRequested(bool ShowTimestamps) : ConfigurationChange;
public record EnabledChannelsChangeRequested(HashSet<XivChatType> EnabledChannels) : ConfigurationChange;
public record ChannelToggleRequested(XivChatType Channel) : ConfigurationChange;
public record ResetChannelsRequested : ConfigurationChange;

// Configuration updated event - broadcasted after configuration is actually changed
public record ConfigurationUpdated(ChatModuleConfiguration Configuration);

