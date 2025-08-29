using System;
using System.Threading.Tasks;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;
using Dalamud.Game.Text.SeStringHandling;
using Microsoft.Extensions.DependencyInjection;
using TLink.Core.Module;
using TLink.Core.MVU;
using TLink.Modules.Chat.Models;

namespace TLink.Modules.Chat;

[ModuleInfo("Chat", "2.0.0", Description = "Chat selector for translation", Author = "DN5S")]
public class ChatModule : ModuleBase
{
    private ChatViewModel? viewModel;
    private Store<ChatState>? store;
    private IChatGui? chatGui;
    private ChatModuleConfiguration? moduleConfig;
    
    public override string Name => "Chat";
    public override string Version => "2.0.0";
    
    public override void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<IStore<ChatState>>(_ =>
        {
            var initialState = ChatState.Initial;
            store = new Store<ChatState>(initialState, ChatUpdate.Update);
            
            store.RegisterEffectHandler(new SaveConfigurationEffectHandler(SetModuleConfig));

            store.UseMiddleware(async (state, action, next) =>
            {
                Logger.Debug($"Action dispatched: {action.Type}");
                await next();
                Logger.Debug($"State updated: Version {state.Version}");
            });

            return store;
        });
        
        services.AddSingleton<ChatViewModel>();
    }
    
    protected override void LoadConfiguration()
    {
        moduleConfig = GetModuleConfig<ChatModuleConfiguration>();
    }
    
    public override void Initialize()
    {
        chatGui = Services.GetRequiredService<IChatGui>();
        store = (Store<ChatState>)Services.GetRequiredService<IStore<ChatState>>();
        viewModel = Services.GetRequiredService<ChatViewModel>();
        
        // Initialize viewModel with store
        viewModel.Initialize(store);
        
        // Use async dispatch to avoid deadlock during initialization
        _ = Task.Run(async () => 
        {
            await store.DispatchAsync(new LoadConfigurationAction(moduleConfig!)).ConfigureAwait(false);
        });
        
        chatGui.ChatMessage += OnChatMessage;
        
        Logger.Information($"Chat module initialized with MVU pattern. Translatable channels: [{string.Join(", ", store.State.TranslatableChannels)}]");
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
        
        Logger.Debug($"Chat: Received message - Type: {type}, Sender: {sender.TextValue}, Message: {message.TextValue}");
        
        // Check if this message type should be translated
        if (store?.State.TranslatableChannels.Contains(type) == true)
        {
            Logger.Information($"Chat: Publishing TranslatableMessageReceived for {type} message from {sender.TextValue}");
            EventBus.Publish(new TranslatableMessageReceived(chatMessage));
        }
        else
        {
            Logger.Debug($"Chat: Message type {type} is not marked for translation. Enabled channels: [{string.Join(", ", store?.State.TranslatableChannels ?? [])}]");
        }
        
        // Still publish the general event for other modules that might need it
        EventBus.Publish(new ChatMessageReceived(chatMessage));
    }
    
    public override void DrawUI()
    {
        // No standalone UI for simplified Chat module
    }
    
    public override void DrawConfiguration()
    {
        viewModel?.DrawConfigurationUI();
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
        GC.SuppressFinalize(this);
    }
}
