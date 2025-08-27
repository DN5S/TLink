using System;
using System.Collections.Immutable;
using System.Linq;
using Dalamud.Game.Text;
using SamplePlugin.Core.MVU;
using SamplePlugin.Modules.Chat.Models;

namespace SamplePlugin.Modules.Chat;

public static class ChatUpdate
{
    public static UpdateResult<ChatState> Update(ChatState state, IAction action)
    {
        return action switch
        {
            AddMessageAction addMsg => HandleAddMessage(state, addMsg),
            SetFilterAction setFilter => HandleSetFilter(state, setFilter),
            ToggleChannelAction toggleChannel => HandleToggleChannel(state, toggleChannel),
            ClearMessagesAction => HandleClearMessages(state),
            UpdateMaxMessagesAction updateMax => HandleUpdateMaxMessages(state, updateMax),
            UpdateAutoScrollAction updateScroll => HandleUpdateAutoScroll(state, updateScroll),
            UpdateShowTimestampsAction updateTimestamps => HandleUpdateShowTimestamps(state, updateTimestamps),
            ResetChannelFiltersAction => HandleResetChannelFilters(state),
            LoadConfigurationAction loadConfig => HandleLoadConfiguration(state, loadConfig),
            _ => UpdateResult<ChatState>.NoChange(state)
        };
    }
    
    private static UpdateResult<ChatState> HandleAddMessage(ChatState state, AddMessageAction action)
    {
        var messages = state.Messages.Add(action.Message);
        
        if (messages.Count > state.MaxMessages)
        {
            messages = messages.RemoveRange(0, messages.Count - state.MaxMessages);
        }
        
        return UpdateResult<ChatState>.StateOnly(state with { Messages = messages });
    }
    
    private static UpdateResult<ChatState> HandleSetFilter(ChatState state, SetFilterAction action)
    {
        return state.Filter == action.FilterText ? UpdateResult<ChatState>.NoChange(state) :
                   UpdateResult<ChatState>.StateOnly(state with { Filter = action.FilterText });
    }
    
    private static UpdateResult<ChatState> HandleToggleChannel(ChatState state, ToggleChannelAction action)
    {
        var channels = state.EnabledChannels.Contains(action.Channel)
            ? state.EnabledChannels.Remove(action.Channel)
            : state.EnabledChannels.Add(action.Channel);
            
        var newState = state with { EnabledChannels = channels };
        var config = CreateConfigurationFromState(newState);
        
        return UpdateResult<ChatState>.WithEffects(
            newState,
            new SaveConfigurationEffect(config),
            new NotifyConfigurationChangedEffect(config)
        );
    }
    
    private static UpdateResult<ChatState> HandleClearMessages(ChatState state)
    {
        return UpdateResult<ChatState>.StateOnly(state with { Messages = ImmutableList<ChatMessage>.Empty });
    }
    
    private static UpdateResult<ChatState> HandleUpdateMaxMessages(ChatState state, UpdateMaxMessagesAction action)
    {
        var maxMessages = Math.Clamp(action.MaxMessages, 100, 10000);
        if (state.MaxMessages == maxMessages)
            return UpdateResult<ChatState>.NoChange(state);
        
        var messages = state.Messages;
        if (messages.Count > maxMessages)
        {
            messages = messages.RemoveRange(0, messages.Count - maxMessages);
        }
        
        var newState = state with 
        { 
            MaxMessages = maxMessages,
            Messages = messages
        };
        
        var config = CreateConfigurationFromState(newState);
        
        return UpdateResult<ChatState>.WithEffects(
            newState,
            new SaveConfigurationEffect(config),
            new NotifyConfigurationChangedEffect(config)
        );
    }
    
    private static UpdateResult<ChatState> HandleUpdateAutoScroll(ChatState state, UpdateAutoScrollAction action)
    {
        if (state.AutoScroll == action.AutoScroll)
            return UpdateResult<ChatState>.NoChange(state);
            
        var newState = state with { AutoScroll = action.AutoScroll };
        var config = CreateConfigurationFromState(newState);
        
        return UpdateResult<ChatState>.WithEffects(
            newState,
            new SaveConfigurationEffect(config),
            new NotifyConfigurationChangedEffect(config)
        );
    }
    
    private static UpdateResult<ChatState> HandleUpdateShowTimestamps(ChatState state, UpdateShowTimestampsAction action)
    {
        if (state.ShowTimestamps == action.ShowTimestamps)
            return UpdateResult<ChatState>.NoChange(state);
            
        var newState = state with { ShowTimestamps = action.ShowTimestamps };
        var config = CreateConfigurationFromState(newState);
        
        return UpdateResult<ChatState>.WithEffects(
            newState,
            new SaveConfigurationEffect(config),
            new NotifyConfigurationChangedEffect(config)
        );
    }
    
    private static UpdateResult<ChatState> HandleResetChannelFilters(ChatState state)
    {
        var defaultChannels = ImmutableHashSet.Create(
            XivChatType.Say,
            XivChatType.Shout,
            XivChatType.Party,
            XivChatType.Alliance,
            XivChatType.FreeCompany
        );
        
        if (state.EnabledChannels.SetEquals(defaultChannels))
            return UpdateResult<ChatState>.NoChange(state);
            
        var newState = state with { EnabledChannels = defaultChannels };
        var config = CreateConfigurationFromState(newState);
        
        return UpdateResult<ChatState>.WithEffects(
            newState,
            new SaveConfigurationEffect(config),
            new NotifyConfigurationChangedEffect(config)
        );
    }
    
    private static UpdateResult<ChatState> HandleLoadConfiguration(ChatState state, LoadConfigurationAction action)
    {
        var config = action.Configuration;
        
        var messages = state.Messages;
        if (messages.Count > config.MaxMessages)
        {
            messages = messages.RemoveRange(0, messages.Count - config.MaxMessages);
        }
        
        return UpdateResult<ChatState>.StateOnly(state with
        {
            EnabledChannels = config.EnabledChannels.ToImmutableHashSet(),
            MaxMessages = config.MaxMessages,
            AutoScroll = config.AutoScroll,
            ShowTimestamps = config.ShowTimestamps,
            Messages = messages
        });
    }
    
    private static ChatModuleConfiguration CreateConfigurationFromState(ChatState state)
    {
        return new ChatModuleConfiguration
        {
            ModuleName = "Chat",
            EnabledChannels = state.EnabledChannels.ToHashSet(),
            MaxMessages = state.MaxMessages,
            AutoScroll = state.AutoScroll,
            ShowTimestamps = state.ShowTimestamps
        };
    }
}
