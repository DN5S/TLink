namespace TLink.Modules.Chat.Models;

// Event published when a message is received that should be processed
public record MessageReceivedEvent(ChatMessage Message);