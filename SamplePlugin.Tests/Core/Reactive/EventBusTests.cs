using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using SamplePlugin.Core.Reactive;
using Xunit;

namespace SamplePlugin.Tests.Core.Reactive;

public class EventBusTests : IDisposable
{
    private readonly EventBus eventBus = new();

    [Fact]
    public void Publish_WithValidMessage_ShouldBeReceivedBySubscribers()
    {
        // Arrange
        var received = new List<TestMessage>();
        using var subscription = eventBus.Listen<TestMessage>()
            .Subscribe(msg => received.Add(msg));
        
        var message = new TestMessage { Content = "Test" };
        
        // Act
        eventBus.Publish(message);
        
        // Assert
        received.Should().ContainSingle()
            .Which.Content.Should().Be("Test");
    }
    
    [Fact]
    public void Publish_ToMultipleSubscribers_ShouldBeReceivedByAll()
    {
        // Arrange
        var received1 = new List<TestMessage>();
        var received2 = new List<TestMessage>();
        
        using var sub1 = eventBus.Listen<TestMessage>()
            .Subscribe(msg => received1.Add(msg));
        using var sub2 = eventBus.Listen<TestMessage>()
            .Subscribe(msg => received2.Add(msg));
        
        var message = new TestMessage { Content = "Test" };
        
        // Act
        eventBus.Publish(message);
        
        // Assert
        received1.Should().ContainSingle();
        received2.Should().ContainSingle();
    }
    
    [Fact]
    public void Publish_DifferentMessageTypes_ShouldBeFilteredCorrectly()
    {
        // Arrange
        var testMessages = new List<TestMessage>();
        var otherMessages = new List<OtherMessage>();
        
        using var sub1 = eventBus.Listen<TestMessage>()
            .Subscribe(msg => testMessages.Add(msg));
        using var sub2 = eventBus.Listen<OtherMessage>()
            .Subscribe(msg => otherMessages.Add(msg));
        
        // Act
        eventBus.Publish(new TestMessage { Content = "Test" });
        eventBus.Publish(new OtherMessage { Data = 42 });
        
        // Assert
        testMessages.Should().ContainSingle()
            .Which.Content.Should().Be("Test");
        otherMessages.Should().ContainSingle()
            .Which.Data.Should().Be(42, "Verifying Data property is correctly set and retrieved");
    }
    
    [Fact]
    public void ListenLatest_ShouldReceiveInitialValue()
    {
        // Arrange
        var received = new List<TestMessage>();
        var initialMessage = new TestMessage { Content = "Initial" };
        
        // Act
        using var subscription = eventBus.ListenLatest(initialMessage)
            .Subscribe(msg => received.Add(msg));
        
        // Assert
        received.Should().ContainSingle()
            .Which.Content.Should().Be("Initial");
    }
    
    [Fact]
    public void ListenLatest_WithPublishedMessage_ShouldReceiveBoth()
    {
        // Arrange
        var received = new List<TestMessage>();
        var initialMessage = new TestMessage { Content = "Initial" };
        
        using var subscription = eventBus.ListenLatest(initialMessage)
            .Subscribe(msg => received.Add(msg));
        
        // Act
        eventBus.Publish(new TestMessage { Content = "Published" });
        
        // Assert
        received.Should().HaveCount(2);
        received[0].Content.Should().Be("Initial");
        received[1].Content.Should().Be("Published");
    }
    
    [Fact]
    public void ListenWithReplay_ShouldReplayLastMessage()
    {
        // Arrange - Must set up a replay listener before publishing
        var replay = eventBus.ListenWithReplay<TestMessage>();
        
        eventBus.Publish(new TestMessage { Content = "First" });
        eventBus.Publish(new TestMessage { Content = "Second" });
        
        // Act
        var received = new List<TestMessage>();
        using var subscription = replay.Subscribe(msg => received.Add(msg));
        
        // Assert
        received.Should().ContainSingle()
            .Which.Content.Should().Be("Second");
    }
    
    [Fact]
    public void ListenWithReplay_WithBufferSize_ShouldReplayMultipleMessages()
    {
        // Arrange - Must set up a replay listener before publishing
        var replay = eventBus.ListenWithReplay<TestMessage>(bufferSize: 2);
        
        eventBus.Publish(new TestMessage { Content = "First" });
        eventBus.Publish(new TestMessage { Content = "Second" });
        eventBus.Publish(new TestMessage { Content = "Third" });
        
        // Act
        var received = new List<TestMessage>();
        using var subscription = replay.Subscribe(msg => received.Add(msg));
        
        // Assert
        received.Should().HaveCount(2);
        received[0].Content.Should().Be("Second");
        received[1].Content.Should().Be("Third");
    }
    
    [Fact]
    public void ClearReplayBuffer_ShouldRemoveBufferedMessages()
    {
        // Arrange
        eventBus.Publish(new TestMessage { Content = "Buffered" });
        
        // Act
        eventBus.ClearReplayBuffer<TestMessage>();
        
        var received = new List<TestMessage>();
        using var subscription = eventBus.ListenWithReplay<TestMessage>()
            .Subscribe(msg => received.Add(msg));
        
        // Assert
        received.Should().BeEmpty();
    }
    
    [Fact]
    public void Publish_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        eventBus.Dispose();
        
        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => 
            eventBus.Publish(new TestMessage { Content = "Test" }));
    }
    
    [Fact]
    public void Listen_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        eventBus.Dispose();
        
        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => 
            eventBus.Listen<TestMessage>());
    }
    
    [Fact]
    public void ListenLatest_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        eventBus.Dispose();
        
        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => 
            eventBus.ListenLatest(new TestMessage()));
    }
    
    [Fact]
    public void ListenWithReplay_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        eventBus.Dispose();
        
        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => 
            eventBus.ListenWithReplay<TestMessage>());
    }
    
    [Fact]
    public void Dispose_MultipleTimes_ShouldNotThrow()
    {
        // Act & Assert
        eventBus.Dispose();
        eventBus.Dispose(); // Should not throw
    }
    
    [Fact]
    public async Task Publish_WithConcurrentPublishers_ShouldBeThreadSafe()
    {
        // Arrange
        var received = new List<TestMessage>();
        using var subscription = eventBus.Listen<TestMessage>()
            .Subscribe(msg => 
            {
                lock (received)
                {
                    received.Add(msg);
                }
            });
        
        var tasks = new List<Task>();
        
        // Act
        for (var i = 0; i < 100; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() => 
                eventBus.Publish(new TestMessage { Content = $"Message {index}" })));
        }
        
        await Task.WhenAll(tasks);
        await Task.Delay(100); // Give time for all messages to be processed
        
        // Assert
        received.Should().HaveCount(100);
    }
    
    [Fact]
    public void Subscription_WhenDisposed_ShouldStopReceivingMessages()
    {
        // Arrange
        var received = new List<TestMessage>();
        var subscription = eventBus.Listen<TestMessage>()
            .Subscribe(msg => received.Add(msg));
        
        eventBus.Publish(new TestMessage { Content = "First" });
        subscription.Dispose();
        
        // Act
        eventBus.Publish(new TestMessage { Content = "Second" });
        
        // Assert
        received.Should().ContainSingle()
            .Which.Content.Should().Be("First");
    }
    
    public void Dispose()
    {
        eventBus?.Dispose();
        GC.SuppressFinalize(this);
    }
    
    // Test helper classes
    private class TestMessage
    {
        public string Content { get; set; } = string.Empty;
    }
    
    private class OtherMessage
    {
        public int Data { get; set; }
    }
}
