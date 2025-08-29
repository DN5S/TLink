using System;
using TLink.Core.Configuration;

namespace TLink.Modules.ChatEcho;

[Serializable]
public class ChatEchoConfig : ModuleConfiguration
{
    public bool Enabled { get; set; } = true;
    public string OutputFormat { get; set; } = "[{time}]<{sender}> {translated}";
    
    public ChatEchoConfig()
    {
        ModuleName = "ChatEcho";
    }
}