using System;
using System.Threading.Tasks;
using ModuleKit.MVU;
using TLink.Modules.Chat.Models;

namespace TLink.Modules.Chat;

public class SaveConfigurationEffectHandler : IEffectHandler<SaveConfigurationEffect>
{
    private readonly Action<ChatModuleConfiguration> saveConfig;
    private readonly Func<ChatState> getCurrentState;
    
    public SaveConfigurationEffectHandler(Action<ChatModuleConfiguration> saveConfig, Func<ChatState> getCurrentState)
    {
        this.saveConfig = saveConfig;
        this.getCurrentState = getCurrentState;
    }
    
    public Task HandleAsync(SaveConfigurationEffect effect, IStore store)
    {
        var state = getCurrentState();
        var config = new ChatModuleConfiguration
        {
            EnabledChannels = new(state.EnabledChannels),
            MaxMessageHistory = state.MaxMessageHistory,
            IsEnabled = state.IsEnabled
        };
        
        saveConfig(config);
        return Task.CompletedTask;
    }
}