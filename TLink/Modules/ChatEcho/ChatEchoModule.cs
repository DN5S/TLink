using System;
using System.Text.RegularExpressions;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using TLink.Core.Module;
using TLink.Modules.Translation.Models;

namespace TLink.Modules.ChatEcho;

[ModuleInfo("ChatEcho", "1.0.0", 
    Dependencies = ["Translation"],
    Description = "Outputs translated messages to game chat",
    Author = "DN5S",
    Priority = 20)]
public partial class ChatEchoModule : ModuleBase
{
    private IChatGui? chatGui;
    private ChatEchoConfig? moduleConfig;
    
    public override string Name => "ChatEcho";
    public override string Version => "1.0.0";
    public override string[] Dependencies => ["Translation"];
    
    protected override void LoadConfiguration()
    {
        moduleConfig = GetModuleConfig<ChatEchoConfig>();
    }
    
    public override void RegisterServices(IServiceCollection services)
    {
        
    }
    
    public override void Initialize()
    {
        chatGui = Services.GetRequiredService<IChatGui>();
        
        // Subscribe to translation results
        Subscriptions.Add(
            EventBus.Listen<MessageTranslatedEvent>()
                .Subscribe(evt =>
                {
                    if (!moduleConfig?.Enabled ?? false)
                        return;
                    
                    var output = FormatTranslation(evt);
                    chatGui.Print(output);
                    
                    Logger.Debug($"Echoed translation to chat: {evt.OriginalMessage.Message} -> {evt.TranslatedResult.TranslatedText}");
                })
        );
        
        Logger.Information("ChatEcho module initialized");
    }
    
    private SeString FormatTranslation(MessageTranslatedEvent evt)
    {
        var format = moduleConfig?.OutputFormat ?? "{translated}";
        var builder = new SeStringBuilder();
        var placeholderRegex = PlaceHolderRegex();
        var lastIndex = 0;

        foreach (Match match in placeholderRegex.Matches(format))
        {
            if (match.Index > lastIndex)
            {
                builder.AddText(format.Substring(lastIndex, match.Index - lastIndex));
            }

            var placeholder = match.Groups[1].Value.ToLowerInvariant();
            switch (placeholder)
            {
                case "original":
                    // Preserve original message formatting
                    builder.Append(evt.OriginalMessage.SeStringMessage);
                    break;
                case "translated":
                    // Preserve translated message formatting
                    builder.Append(evt.TranslatedResult.TranslatedSeString);
                    break;
                case "sender":
                    // Preserve sender name formatting
                    builder.Append(evt.OriginalMessage.SeStringSender);
                    break;
                case "language":
                    builder.AddText(evt.TranslatedResult.DetectedLanguage ?? "?");
                    break;
                case "time":
                    builder.AddText(evt.OriginalMessage.Timestamp.ToString("HH:mm"));
                    break;
                default:
                    builder.AddText(match.Value);
                    break;
            }

            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < format.Length)
        {
            builder.AddText(format[lastIndex..]);
        }

        return builder.Build();
    }
    
    public override void DrawUI()
    {
        
    }
    
    public override void DrawConfiguration()
    {
        DrawConfigurationUI();
    }
    
    private void DrawConfigurationUI()
    {
        if (moduleConfig == null) return;
        
        var enabled = moduleConfig.Enabled;
        if (ImGui.Checkbox("Enable chat echo", ref enabled))
        {
            moduleConfig.Enabled = enabled;
            SetModuleConfig(moduleConfig);
        }
        
        ImGui.Spacing();
        ImGui.Text("Output format:");
        
        var format = moduleConfig.OutputFormat;
        if (ImGui.InputText("##Format", ref format, 256))
        {
            moduleConfig.OutputFormat = format;
            SetModuleConfig(moduleConfig);
        }
        
        ImGui.TextWrapped("Available placeholders:");
        ImGui.BulletText("{original} - Original message");
        ImGui.BulletText("{translated} - Translated text");
        ImGui.BulletText("{sender} - Message sender");
        ImGui.BulletText("{language} - Detected language");
        ImGui.BulletText("{time} - Message time");
        
        ImGui.Spacing();
        
        if (ImGui.Button("Reset to Default"))
        {
            moduleConfig.OutputFormat = "[{time}]<{sender}> {translated}";
            SetModuleConfig(moduleConfig);
        }
    }
    
    public override void Dispose()
    {
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    [GeneratedRegex(@"\{(\w+)\}", RegexOptions.Compiled)]
    private static partial Regex PlaceHolderRegex();
}
