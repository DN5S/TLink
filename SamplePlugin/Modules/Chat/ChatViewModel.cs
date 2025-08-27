using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Dalamud.Game.Text;
using SamplePlugin.Core.MVU;
using SamplePlugin.Modules.Chat.Models;

namespace SamplePlugin.Modules.Chat;

public class ChatViewModel : IDisposable
{
    private readonly IStore<ChatState> store;
    private readonly BehaviorSubject<string> filterSubject = new(string.Empty);
    private IDisposable? stateSubscription;
    private IDisposable? filterSubscription;
    
    public ObservableCollection<ChatMessage> Messages { get; } = [];

    public IObservable<string> Filter => filterSubject.AsObservable();
    
    public int MaxMessages => store.State.MaxMessages;
    public bool AutoScroll => store.State.AutoScroll;
    public bool ShowTimestamps => store.State.ShowTimestamps;
    public bool IsChannelEnabled(XivChatType channel) => store.State.EnabledChannels.Contains(channel);
    
    public ChatViewModel(IStore<ChatState> chatStore)
    {
        store = chatStore;
        
        stateSubscription = store.StateChanged
            .Subscribe(OnStateChanged);
            
        filterSubscription = filterSubject
            .Throttle(TimeSpan.FromMilliseconds(300))
            .Subscribe(filter => store.Dispatch(new SetFilterAction(filter)));
            
        UpdateMessages(store.State);
    }
    
    public void ProcessAction(IAction action)
    {
        if (action is SetFilterAction setFilter)
        {
            filterSubject.OnNext(setFilter.FilterText);
        }
        else
        {
            store.Dispatch(action);
        }
    }
    
    public void SetFilter(string filter)
    {
        filterSubject.OnNext(filter);
    }
    
    private void OnStateChanged(ChatState state)
    {
        UpdateMessages(state);
    }
    
    private void UpdateMessages(ChatState state)
    {
        Messages.Clear();
        var messagesToDisplay = state.FilteredMessages.TakeLast(state.MaxMessages);
        foreach (var message in messagesToDisplay)
        {
            Messages.Add(message);
        }
    }
    
    public void Dispose()
    {
        stateSubscription?.Dispose();
        filterSubscription?.Dispose();
        filterSubject.Dispose();
        GC.SuppressFinalize(this);
    }
}
