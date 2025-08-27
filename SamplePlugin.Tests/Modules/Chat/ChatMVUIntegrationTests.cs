using System;
using System.Threading.Tasks;
using Dalamud.Game.Text;
using Microsoft.Extensions.DependencyInjection;
using SamplePlugin.Core.MVU;
using SamplePlugin.Core.Reactive;
using SamplePlugin.Modules.Chat;
using SamplePlugin.Modules.Chat.Models;
using Xunit;

namespace SamplePlugin.Tests.Modules.Chat;

public class ChatMvuIntegrationTests : IDisposable
{
    private readonly IServiceProvider serviceProvider;
    private readonly EventBus eventBus;
    private readonly IStore<ChatState> store;
    private readonly ChatViewModel viewModel;
    
    public ChatMvuIntegrationTests()
    {
        var services = new ServiceCollection();
        eventBus = new EventBus();
        services.AddSingleton(eventBus);
        
        var initialState = ChatState.Initial;
        store = new Store<ChatState>(initialState, ChatUpdate.Update);
        
        services.AddSingleton(store);
        services.AddSingleton<ChatViewModel>();
        
        serviceProvider = services.BuildServiceProvider();
        viewModel = serviceProvider.GetRequiredService<ChatViewModel>();
    }
    
    [Fact]
    public async Task ProcessAction_SetFilterAction_UpdatesFilter()
    {
        const string filterText = "test filter";
        var stateChanged = false;
        
        using var subscription = store.StateChanged.Subscribe(state =>
        {
            if (state.Filter == filterText)
                stateChanged = true;
        });
        
        viewModel.ProcessAction(new SetFilterAction(filterText));
        
        await Task.Delay(400);
        
        Assert.True(stateChanged);
        Assert.Equal(filterText, store.State.Filter);
    }
    
    [Fact]
    public void ProcessAction_ToggleChannelAction_UpdatesEnabledChannels()
    {
        var channel = XivChatType.Shout;
        var initiallyEnabled = store.State.EnabledChannels.Contains(channel);
        
        viewModel.ProcessAction(new ToggleChannelAction(channel));
        
        Assert.NotEqual(initiallyEnabled, store.State.EnabledChannels.Contains(channel));
    }
    
    [Fact]
    public void ProcessAction_ClearMessagesAction_ClearsAllMessages()
    {
        store.Dispatch(new AddMessageAction(new ChatMessage
        {
            Type = XivChatType.Say,
            Timestamp = DateTime.Now,
            Sender = "Player1",
            Message = "Hello"
        }));
        
        Assert.Single(store.State.Messages);
        
        viewModel.ProcessAction(new ClearMessagesAction());
        
        Assert.Empty(store.State.Messages);
    }
    
    [Fact]
    public void ProcessAction_UpdateMaxMessagesAction_UpdatesMaxMessages()
    {
        const int newMaxMessages = 500;
        
        viewModel.ProcessAction(new UpdateMaxMessagesAction(newMaxMessages));
        
        Assert.Equal(newMaxMessages, store.State.MaxMessages);
    }
    
    [Fact]
    public void ProcessAction_UpdateAutoScrollAction_UpdatesAutoScroll()
    {
        var initialAutoScroll = store.State.AutoScroll;
        
        viewModel.ProcessAction(new UpdateAutoScrollAction(!initialAutoScroll));
        
        Assert.NotEqual(initialAutoScroll, store.State.AutoScroll);
    }
    
    [Fact]
    public void ProcessAction_UpdateShowTimestampsAction_UpdatesShowTimestamps()
    {
        var initialShowTimestamps = store.State.ShowTimestamps;
        
        viewModel.ProcessAction(new UpdateShowTimestampsAction(!initialShowTimestamps));
        
        Assert.NotEqual(initialShowTimestamps, store.State.ShowTimestamps);
    }
    
    [Fact]
    public void ProcessAction_ResetChannelFiltersAction_ResetsToDefaults()
    {
        store.Dispatch(new ToggleChannelAction(XivChatType.Say));
        store.Dispatch(new ToggleChannelAction(XivChatType.Shout));
        
        viewModel.ProcessAction(new ResetChannelFiltersAction());
        
        Assert.Contains(XivChatType.Say, store.State.EnabledChannels);
        Assert.Contains(XivChatType.Shout, store.State.EnabledChannels);
        Assert.Contains(XivChatType.Party, store.State.EnabledChannels);
        Assert.Contains(XivChatType.Alliance, store.State.EnabledChannels);
        Assert.Contains(XivChatType.FreeCompany, store.State.EnabledChannels);
    }
    
    [Fact]
    public void Store_MaintainsStateImmutability()
    {
        var originalState = store.State;
        var originalVersion = originalState.Version;
        
        store.Dispatch(new AddMessageAction(new ChatMessage
        {
            Type = XivChatType.Say,
            Timestamp = DateTime.Now,
            Sender = "Player1",
            Message = "Test"
        }));
        
        var newState = store.State;
        
        Assert.NotSame(originalState, newState);
        Assert.NotEqual(originalVersion, newState.Version);
        Assert.Empty(originalState.Messages);
        Assert.Single(newState.Messages);
    }
    
    [Fact]
    public void Store_TracksVersionCorrectly()
    {
        var version1 = store.State.Version;
        
        store.Dispatch(new AddMessageAction(new ChatMessage
        {
            Type = XivChatType.Say,
            Timestamp = DateTime.Now,
            Sender = "Player1",
            Message = "Message 1"
        }));
        
        var version2 = store.State.Version;
        
        store.Dispatch(new AddMessageAction(new ChatMessage
        {
            Type = XivChatType.Say,
            Timestamp = DateTime.Now,
            Sender = "Player2",
            Message = "Message 2"
        }));
        
        var version3 = store.State.Version;
        
        Assert.True(version3 > version2);
        Assert.True(version2 > version1);
    }
    
    [Fact]
    public void FilteredMessages_FiltersCorrectly()
    {
        store.Dispatch(new LoadConfigurationAction(new ChatModuleConfiguration
        {
            EnabledChannels = [XivChatType.Say, XivChatType.Party]
        }));
        
        store.Dispatch(new AddMessageAction(new ChatMessage
        {
            Type = XivChatType.Say,
            Timestamp = DateTime.Now,
            Sender = "Player1",
            Message = "Say message"
        }));
        
        store.Dispatch(new AddMessageAction(new ChatMessage
        {
            Type = XivChatType.Shout,
            Timestamp = DateTime.Now,
            Sender = "Player2",
            Message = "Shout message"
        }));
        
        store.Dispatch(new AddMessageAction(new ChatMessage
        {
            Type = XivChatType.Party,
            Timestamp = DateTime.Now,
            Sender = "Player3",
            Message = "Party message"
        }));
        
        var filtered = store.State.FilteredMessages;
        Assert.Equal(2, System.Linq.Enumerable.Count(filtered));
        
        store.Dispatch(new SetFilterAction("Party"));
        filtered = store.State.FilteredMessages;
        Assert.Single(filtered);
    }
    
    [Fact]
    public void UnidirectionalDataFlow_CompleteScenario()
    {
        var actionDispatched = false;
        var stateChanged = false;
        
        using var actionSubscription = store.ActionDispatched
            .Subscribe(_ => actionDispatched = true);
        
        using var stateSubscription = store.StateChanged
            .Subscribe(_ => stateChanged = true);
        
        viewModel.ProcessAction(new UpdateMaxMessagesAction(2000));
        
        Task.Delay(100);
        
        Assert.True(actionDispatched, "Action should be dispatched");
        Assert.True(stateChanged, "State should be changed");
        Assert.Equal(2000, store.State.MaxMessages);
        Assert.Equal(2000, viewModel.MaxMessages);
    }
    
    [Fact]
    public void Middleware_ExecutesInOrder()
    {
        var executionOrder = "";
        var testStore = new Store<ChatState>(ChatState.Initial, ChatUpdate.Update);
        
        testStore.UseMiddleware(async (_, _, next) =>
        {
            executionOrder += "1";
            await next();
            executionOrder += "4";
        });
        
        testStore.UseMiddleware(async (_, _, next) =>
        {
            executionOrder += "2";
            await next();
            executionOrder += "3";
        });
        
        testStore.Dispatch(new ClearMessagesAction());
        
        Assert.Equal("1234", executionOrder);
    }
    
    public void Dispose()
    {
        viewModel?.Dispose();
        eventBus?.Dispose();
        (store as IDisposable)?.Dispose();
        
        if (serviceProvider is IDisposable disposable)
            disposable.Dispose();
            
        GC.SuppressFinalize(this);
    }
}
