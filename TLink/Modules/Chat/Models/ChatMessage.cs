using System;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;

namespace TLink.Modules.Chat.Models;

public record ChatMessage
{
    public XivChatType Type { get; init; }
    public DateTime Timestamp { get; init; }
    public string Sender { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public SeString SeStringSender { get; init; } = new();
    public SeString SeStringMessage { get; init; } = new();
}