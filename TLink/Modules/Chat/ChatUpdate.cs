using System.Collections.Immutable;
using System.Linq;
using TLink.Core.MVU;
using TLink.Modules.Chat.Models;

namespace TLink.Modules.Chat;

public static class ChatUpdate
{
    public static UpdateResult<ChatState> Update(ChatState state, IAction action)
    {
        return action switch
        {
            ToggleTranslatableChannelAction toggleChannel => HandleToggleTranslatableChannel(state, toggleChannel),
            SetTranslatableChannelsAction setChannels => HandleSetTranslatableChannels(state, setChannels),
            ResetTranslatableChannelsAction => HandleResetTranslatableChannels(state),
            LoadConfigurationAction loadConfig => HandleLoadConfiguration(state, loadConfig),
            _ => UpdateResult<ChatState>.NoChange(state)
        };
    }
    
    private static UpdateResult<ChatState> HandleToggleTranslatableChannel(ChatState state, ToggleTranslatableChannelAction action)
    {
        var channels = state.TranslatableChannels.Contains(action.Channel)
            ? state.TranslatableChannels.Remove(action.Channel)
            : state.TranslatableChannels.Add(action.Channel);
            
        var newState = state with { TranslatableChannels = channels };
        var config = CreateConfigurationFromState(newState);
        
        return UpdateResult<ChatState>.WithEffects(
            newState,
            new SaveConfigurationEffect(config)
        );
    }
    
    private static UpdateResult<ChatState> HandleSetTranslatableChannels(ChatState state, SetTranslatableChannelsAction action)
    {
        var channels = action.Channels.ToImmutableHashSet();
        if (state.TranslatableChannels.SetEquals(channels))
            return UpdateResult<ChatState>.NoChange(state);
            
        var newState = state with { TranslatableChannels = channels };
        var config = CreateConfigurationFromState(newState);
        
        return UpdateResult<ChatState>.WithEffects(
            newState,
            new SaveConfigurationEffect(config)
        );
    }
    
    private static UpdateResult<ChatState> HandleResetTranslatableChannels(ChatState state)
    {
        var defaultChannels = ChatModuleConfiguration.GetDefaultTranslatableChannels().ToImmutableHashSet();
        
        if (state.TranslatableChannels.SetEquals(defaultChannels))
            return UpdateResult<ChatState>.NoChange(state);
            
        var newState = state with { TranslatableChannels = defaultChannels };
        var config = CreateConfigurationFromState(newState);
        
        return UpdateResult<ChatState>.WithEffects(
            newState,
            new SaveConfigurationEffect(config)
        );
    }
    
    private static UpdateResult<ChatState> HandleLoadConfiguration(ChatState state, LoadConfigurationAction action)
    {
        var config = action.Configuration;
        
        return UpdateResult<ChatState>.StateOnly(state with
        {
            TranslatableChannels = config.TranslatableChannels.ToImmutableHashSet()
        });
    }
    
    private static ChatModuleConfiguration CreateConfigurationFromState(ChatState state)
    {
        return new ChatModuleConfiguration
        {
            ModuleName = "Chat",
            TranslatableChannels = state.TranslatableChannels.ToHashSet()
        };
    }
}
