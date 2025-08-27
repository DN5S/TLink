using System;
using Microsoft.Extensions.DependencyInjection;
using SamplePlugin.Core.Module;
using SamplePlugin.Core.MVU;
using SamplePlugin.Modules.Chat.Models;
using Dalamud.Plugin.Services;
using Dalamud.Game.Text;

namespace SamplePlugin.Modules.Chat;

[ModuleInfo("Chat", "1.0.0", Description = "Chat monitoring and filtering module", Author = "Sample Author")]
public class ChatModule : ModuleBase
{
    private ChatWindow? window;
    private ChatViewModel? viewModel;
    private Store<ChatState>? store;
    private IChatGui? chatGui;
    private ChatModuleConfiguration? moduleConfig;
    
    public override string Name => "Chat";
    public override string Version => "1.0.0";
    
    public override void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<IStore<ChatState>>(_ =>
        {
            var initialState = ChatState.Initial;
            store = new Store<ChatState>(initialState, ChatUpdate.Update);
            
            store.RegisterEffectHandler(new SaveConfigurationEffectHandler(SetModuleConfig));
            store.RegisterEffectHandler(new NotifyConfigurationChangedEffectHandler(EventBus));

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
        
        store.Dispatch(new LoadConfigurationAction(moduleConfig!));
        
        window = new ChatWindow(viewModel);
        
        chatGui.ChatMessage += OnChatMessage;
        
        Logger.Information("Chat module initialized with MVU pattern");
    }
    
    private void OnChatMessage(XivChatType type, int timestamp, ref Dalamud.Game.Text.SeStringHandling.SeString sender, ref Dalamud.Game.Text.SeStringHandling.SeString message, ref bool isHandled)
    {
        var chatMessage = new ChatMessage
        {
            Type = type,
            // Use current time instead of the timestamp parameter which may not be reliable
            Timestamp = DateTime.Now,
            Sender = sender.TextValue,
            Message = message.TextValue
        };
        
        store?.Dispatch(new AddMessageAction(chatMessage));
        
        EventBus.Publish(new ChatMessageReceived(chatMessage));
    }
    
    public override void DrawUI()
    {
        window?.Draw();
    }
    
    public override void DrawConfiguration()
    {
        window?.DrawConfiguration();
    }
    
    public override void Dispose()
    {
        if (chatGui != null)
        {
            chatGui.ChatMessage -= OnChatMessage;
        }
        
        window?.Dispose();
        viewModel?.Dispose();
        store?.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
