using System;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.DependencyInjection;
using ModuleKit.Module;
using ModuleKit.MVU;
using ModuleKit.Reactive;
using TLink.Modules.Chat.Models;

namespace TLink.Modules.Chat;

[ModuleInfo("Chat", "1.0.0", Description = "Captures game and system messages", Author = "DN5S")]
public class ChatModule : ModuleBase
{
    private IChatGui? chatGui;
    private Store<ChatState>? store;
    private ChatViewModel? viewModel;
    private ChatModuleConfiguration? moduleConfig;
    
    public override string Name => "Chat";
    public override string Version => "1.0.0";
    
    protected override void LoadConfiguration()
    {
        moduleConfig = GetModuleConfig<ChatModuleConfiguration>();
    }
    
    public override void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<IStore<ChatState>>(_ => new Store<ChatState>(
            ChatState.Initial,
            ChatUpdate.Update
        ));
        
        services.AddSingleton<ChatViewModel>();
    }
    
    public override void Initialize()
    {
        chatGui = Services.GetRequiredService<IChatGui>();
        store = (Store<ChatState>)Services.GetRequiredService<IStore<ChatState>>();
        viewModel = Services.GetRequiredService<ChatViewModel>();
        
        // Register effect handler for saving configuration
        store.RegisterEffectHandler(new SaveConfigurationEffectHandler(
            SetModuleConfig,
            () => store.State
        ));
        
        viewModel.Initialize(store);
        store.Dispatch(new LoadConfigurationAction(moduleConfig!));
        
        // Subscribe to game chat messages
        chatGui.ChatMessage += OnChatMessage;
        
        // Debug: Log all events published by this module
        Subscriptions.Add(
            EventBus.Listen<MessageReceivedEvent>()
                .Subscribe(evt => Logger.Debug($"[Chat] Published MessageReceivedEvent: {evt.Message.Type} - {evt.Message.Message}"))
        );
        
        Logger.Information("Chat module initialized");
    }
    
    private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        var chatMessage = new ChatMessage
        {
            Type = type,
            Timestamp = DateTime.Now,
            Sender = sender.TextValue,
            Message = message.TextValue,
            SeStringSender = new SeString(sender.Payloads),
            SeStringMessage = new SeString(message.Payloads)
        };
        
        // Update our state
        store?.Dispatch(new MessageReceivedAction(chatMessage));
        
        // Check if this message type should be processed
        if (moduleConfig?.EnabledChannels.Contains(type) == true)
        {
            // Publish event for other modules to consume
            EventBus.Publish(new MessageReceivedEvent(chatMessage));
        }
    }
    
    public override void DrawUI()
    {
        viewModel?.DrawUI();
    }
    
    public override void DrawConfiguration()
    {
        viewModel?.DrawConfiguration();
    }
    
    public override void Dispose()
    {
        if (chatGui != null)
        {
            chatGui.ChatMessage -= OnChatMessage;
        }
        
        viewModel?.Dispose();
        store?.Dispose();
        base.Dispose();
    }
}
