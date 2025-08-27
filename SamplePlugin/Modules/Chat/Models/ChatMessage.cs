using System;
using Dalamud.Game.Text;

namespace SamplePlugin.Modules.Chat.Models;

public record ChatMessage
{
    public XivChatType Type { get; init; }
    public DateTime Timestamp { get; init; }
    public string Sender { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string Channel => Type.ToString();
}