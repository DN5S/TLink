using System.Collections.Immutable;
using System.Linq;
using ModuleKit.MVU;
using TLink.Modules.Chat.Models;

namespace TLink.Modules.Chat;

public static class ChatUpdate
{
    public static UpdateResult<ChatState> Update(ChatState state, IAction action)
    {
        return action switch
        {
            MessageReceivedAction a => HandleMessageReceived(state, a),
            LoadConfigurationAction a => HandleLoadConfiguration(state, a),
            ToggleChannelAction a => HandleToggleChannel(state, a),
            SetEnabledChannelsAction a => HandleSetEnabledChannels(state, a),
            SetMaxHistoryAction a => HandleSetMaxHistory(state, a),
            ClearHistoryAction a => HandleClearHistory(state),
            SetEnabledAction a => HandleSetEnabled(state, a),
            _ => UpdateResult<ChatState>.NoChange(state)
        };
    }
    
    private static UpdateResult<ChatState> HandleMessageReceived(ChatState state, MessageReceivedAction action)
    {
        var messages = state.RecentMessages.Add(action.Message);
        
        // Trim to max history
        if (messages.Count > state.MaxMessageHistory)
        {
            messages = messages.Skip(messages.Count - state.MaxMessageHistory).ToImmutableList();
        }
        
        return UpdateResult<ChatState>.StateOnly(
            state with 
            { 
                RecentMessages = messages,
                Version = state.Version + 1
            }
        );
    }
    
    private static UpdateResult<ChatState> HandleLoadConfiguration(ChatState state, LoadConfigurationAction action)
    {
        var config = action.Configuration;
        return UpdateResult<ChatState>.StateOnly(
            state with
            {
                EnabledChannels = config.EnabledChannels.ToImmutableHashSet(),
                MaxMessageHistory = config.MaxMessageHistory,
                IsEnabled = config.IsEnabled,
                Version = state.Version + 1
            }
        );
    }
    
    private static UpdateResult<ChatState> HandleToggleChannel(ChatState state, ToggleChannelAction action)
    {
        var channels = state.EnabledChannels.Contains(action.Channel)
            ? state.EnabledChannels.Remove(action.Channel)
            : state.EnabledChannels.Add(action.Channel);
            
        return UpdateResult<ChatState>.WithEffects(
            state with 
            { 
                EnabledChannels = channels,
                Version = state.Version + 1
            },
            new SaveConfigurationEffect()
        );
    }
    
    private static UpdateResult<ChatState> HandleSetEnabledChannels(ChatState state, SetEnabledChannelsAction action)
    {
        return UpdateResult<ChatState>.WithEffects(
            state with
            {
                EnabledChannels = action.Channels.ToImmutableHashSet(),
                Version = state.Version + 1
            },
            new SaveConfigurationEffect()
        );
    }
    
    private static UpdateResult<ChatState> HandleSetMaxHistory(ChatState state, SetMaxHistoryAction action)
    {
        var messages = state.RecentMessages;
        if (messages.Count > action.MaxHistory)
        {
            messages = messages.Skip(messages.Count - action.MaxHistory).ToImmutableList();
        }
        
        return UpdateResult<ChatState>.WithEffects(
            state with
            {
                MaxMessageHistory = action.MaxHistory,
                RecentMessages = messages,
                Version = state.Version + 1
            },
            new SaveConfigurationEffect()
        );
    }
    
    private static UpdateResult<ChatState> HandleClearHistory(ChatState state)
    {
        return UpdateResult<ChatState>.StateOnly(
            state with
            {
                RecentMessages = ImmutableList<ChatMessage>.Empty,
                Version = state.Version + 1
            }
        );
    }
    
    private static UpdateResult<ChatState> HandleSetEnabled(ChatState state, SetEnabledAction action)
    {
        return UpdateResult<ChatState>.WithEffects(
            state with
            {
                IsEnabled = action.IsEnabled,
                Version = state.Version + 1
            },
            new SaveConfigurationEffect()
        );
    }
}