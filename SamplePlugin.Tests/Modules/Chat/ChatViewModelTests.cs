using System;
using System.Threading.Tasks;
using Dalamud.Game.Text;
using Microsoft.Extensions.DependencyInjection;
using SamplePlugin.Core.MVU;
using SamplePlugin.Modules.Chat;
using SamplePlugin.Modules.Chat.Models;
using Xunit;

namespace SamplePlugin.Tests.Modules.Chat;

public class ChatViewModelTests : IDisposable
{
    private readonly IServiceProvider serviceProvider;
    private readonly IStore<ChatState> store;
    private readonly ChatViewModel viewModel;
    
    public ChatViewModelTests()
    {
        var services = new ServiceCollection();
        
        var initialState = ChatState.Initial;
        store = new Store<ChatState>(initialState, ChatUpdate.Update);
        
        services.AddSingleton(store);
        services.AddSingleton<ChatViewModel>();
        
        serviceProvider = services.BuildServiceProvider();
        viewModel = serviceProvider.GetRequiredService<ChatViewModel>();
    }
    
    [Fact]
    public void Constructor_InitializesWithCorrectDefaults()
    {
        Assert.NotNull(viewModel.Messages);
        Assert.Empty(viewModel.Messages);
        Assert.Equal(1000, viewModel.MaxMessages);
        Assert.True(viewModel.AutoScroll);
        Assert.True(viewModel.ShowTimestamps);
    }
    
    [Fact]
    public void Messages_UpdatesWhenStateChanges()
    {
        store.Dispatch(new AddMessageAction(new ChatMessage
        {
            Type = XivChatType.Say,
            Timestamp = DateTime.Now,
            Sender = "TestSender",
            Message = "Test message"
        }));
        
        Assert.Single(viewModel.Messages);
        Assert.Equal("TestSender", viewModel.Messages[0].Sender);
    }
    
    [Fact]
    public async Task ProcessAction_SetFilter_UpdatesFilter()
    {
        const string testFilter = "test";
        
        viewModel.ProcessAction(new SetFilterAction(testFilter));
        
        await Task.Delay(400);
        
        Assert.Equal(testFilter, store.State.Filter);
    }
    
    [Fact]
    public async Task Messages_FiltersCorrectly()
    {
        store.Dispatch(new AddMessageAction(new ChatMessage
        {
            Type = XivChatType.Say,
            Timestamp = DateTime.Now,
            Sender = "Player1",
            Message = "Hello world"
        }));
        
        store.Dispatch(new AddMessageAction(new ChatMessage
        {
            Type = XivChatType.Say,
            Timestamp = DateTime.Now,
            Sender = "Player2",
            Message = "Goodbye world"
        }));
        
        viewModel.SetFilter("Hello");
        await Task.Delay(400);
        
        Assert.Single(viewModel.Messages);
        Assert.Contains("Hello", viewModel.Messages[0].Message);
    }
    
    [Fact]
    public void Messages_RespectsMaxMessages()
    {
        // Set max messages to minimum allowed value (100)
        store.Dispatch(new UpdateMaxMessagesAction(100));
        Assert.Equal(100, store.State.MaxMessages);
        
        // Add 105 messages
        for (var i = 0; i < 105; i++)
        {
            store.Dispatch(new AddMessageAction(new ChatMessage
            {
                Type = XivChatType.Say,
                Timestamp = DateTime.Now,
                Sender = $"Player{i}",
                Message = $"Message {i}"
            }));
        }
        
        // Check that the state has the correct number of messages (max 100)
        Assert.Equal(100, store.State.Messages.Count);
        // Check that the viewModel also reflects this
        Assert.Equal(100, viewModel.Messages.Count);
        // Check that we kept the last 100 messages (5-104)
        Assert.Equal("Player5", viewModel.Messages[0].Sender);
        Assert.Equal("Player104", viewModel.Messages[99].Sender);
    }
    
    [Fact]
    public void ProcessAction_ClearMessages_ClearsAllMessages()
    {
        store.Dispatch(new AddMessageAction(new ChatMessage
        {
            Type = XivChatType.Say,
            Timestamp = DateTime.Now,
            Sender = "TestSender",
            Message = "Test"
        }));
        
        Assert.Single(viewModel.Messages);
        
        viewModel.ProcessAction(new ClearMessagesAction());
        
        Assert.Empty(viewModel.Messages);
    }
    
    [Fact]
    public void ProcessAction_ToggleChannel_UpdatesChannelState()
    {
        Assert.True(viewModel.IsChannelEnabled(XivChatType.Say));
        
        viewModel.ProcessAction(new ToggleChannelAction(XivChatType.Say));
        
        Assert.False(viewModel.IsChannelEnabled(XivChatType.Say));
        
        viewModel.ProcessAction(new ToggleChannelAction(XivChatType.Say));
        
        Assert.True(viewModel.IsChannelEnabled(XivChatType.Say));
    }
    
    [Fact]
    public void ProcessAction_UpdateMaxMessages_UpdatesMaxMessages()
    {
        Assert.Equal(1000, viewModel.MaxMessages);
        
        viewModel.ProcessAction(new UpdateMaxMessagesAction(500));
        
        Assert.Equal(500, viewModel.MaxMessages);
    }
    
    [Fact]
    public void ProcessAction_UpdateAutoScroll_UpdatesAutoScroll()
    {
        var initial = viewModel.AutoScroll;
        
        viewModel.ProcessAction(new UpdateAutoScrollAction(!initial));
        
        Assert.NotEqual(initial, viewModel.AutoScroll);
    }
    
    [Fact]
    public void ProcessAction_UpdateShowTimestamps_UpdatesShowTimestamps()
    {
        var initial = viewModel.ShowTimestamps;
        
        viewModel.ProcessAction(new UpdateShowTimestampsAction(!initial));
        
        Assert.NotEqual(initial, viewModel.ShowTimestamps);
    }
    
    [Fact]
    public void ProcessAction_ResetChannelFilters_ResetsToDefaults()
    {
        viewModel.ProcessAction(new ToggleChannelAction(XivChatType.Say));
        viewModel.ProcessAction(new ToggleChannelAction(XivChatType.Shout));
        
        Assert.False(viewModel.IsChannelEnabled(XivChatType.Say));
        Assert.False(viewModel.IsChannelEnabled(XivChatType.Shout));
        
        viewModel.ProcessAction(new ResetChannelFiltersAction());
        
        Assert.True(viewModel.IsChannelEnabled(XivChatType.Say));
        Assert.True(viewModel.IsChannelEnabled(XivChatType.Shout));
        Assert.True(viewModel.IsChannelEnabled(XivChatType.Party));
        Assert.True(viewModel.IsChannelEnabled(XivChatType.Alliance));
        Assert.True(viewModel.IsChannelEnabled(XivChatType.FreeCompany));
    }
    
    [Fact]
    public void Messages_FiltersChannelsCorrectly()
    {
        store.Dispatch(new LoadConfigurationAction(new ChatModuleConfiguration
        {
            EnabledChannels = [XivChatType.Say]
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
        
        Assert.Single(viewModel.Messages);
        Assert.Equal(XivChatType.Say, viewModel.Messages[0].Type);
    }
    
    [Fact]
    public void ProcessAction_MultipleActionsInSequence()
    {
        viewModel.ProcessAction(new UpdateMaxMessagesAction(100));
        viewModel.ProcessAction(new UpdateAutoScrollAction(false));
        viewModel.ProcessAction(new UpdateShowTimestampsAction(false));
        
        Assert.Equal(100, viewModel.MaxMessages);
        Assert.False(viewModel.AutoScroll);
        Assert.False(viewModel.ShowTimestamps);
    }
    
    [Fact]
    public void StateChanges_UpdateViewModelProperties()
    {
        var config = new ChatModuleConfiguration
        {
            MaxMessages = 250,
            AutoScroll = false,
            ShowTimestamps = false,
            EnabledChannels = [XivChatType.Party]
        };
        
        store.Dispatch(new LoadConfigurationAction(config));
        
        Assert.Equal(250, viewModel.MaxMessages);
        Assert.False(viewModel.AutoScroll);
        Assert.False(viewModel.ShowTimestamps);
        Assert.True(viewModel.IsChannelEnabled(XivChatType.Party));
        Assert.False(viewModel.IsChannelEnabled(XivChatType.Say));
    }
    
    public void Dispose()
    {
        viewModel?.Dispose();
        (store as IDisposable)?.Dispose();
        
        if (serviceProvider is IDisposable disposable)
            disposable.Dispose();
            
        GC.SuppressFinalize(this);
    }
}
