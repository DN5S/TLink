using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using TLink.Core.MVU;
using TLink.Core.Reactive;
using TLink.Modules.Translation.Models;
using TLink.Modules.Translation.Services;
using Dalamud.Plugin.Services;

namespace TLink.Modules.Translation.MVU;

/// <summary>
/// Handles the execution of the translation pipeline.
/// This is the core of the orchestrator pattern.
/// </summary>
public class PipelineExecutionEffectHandler(
    Func<IReadOnlyList<ITranslationPipelineHandler>> getHandlers,
    EventBus eventBus,
    IPluginLog logger)
    : IEffectHandler<ExecutePipelineEffect>
{
    public async Task HandleAsync(ExecutePipelineEffect effect, IStore baseStore)
    {
        logger.Information($"PipelineExecutionEffectHandler: Starting pipeline for message from {effect.Message.Sender}");
        
        var context = new TranslationContext(effect.Message, effect.SourceLanguage, effect.TargetLanguage);
        var allHandlers = getHandlers();
        logger.Debug($"PipelineExecutionEffectHandler: Total handlers available: {allHandlers.Count}");
        
        var handlers = allHandlers
            .Where(h => h.IsEnabled)
            .OrderBy(h => h.Priority)
            .ToList();
        
        logger.Debug($"PipelineExecutionEffectHandler: Enabled handlers: {handlers.Count}");
        
        if (handlers.Count == 0)
        {
            logger.Warning("No enabled handlers for pipeline execution");
            eventBus.Publish(new TranslationErrorEvent(effect.Message, "No translation handlers available"));
            return;
        }
        
        // Just publish event - avoid nested dispatch that causes deadlock
        eventBus.Publish(new PipelineExecutionStarted(context.RequestId, effect.Message));
        
        var stopwatch = Stopwatch.StartNew();
        var executedHandlers = new List<string>();
        
        try
        {
            // Build the pipeline chain
            await ExecutePipelineAsync(handlers, context, executedHandlers).ConfigureAwait(false);
            
            stopwatch.Stop();
            
            // Determine which handler terminated the pipeline
            var terminatingHandler = context.IsHandled && executedHandlers.Count != 0
                                         ? executedHandlers.Last() 
                                         : null;
            
            // Just publish event - avoid nested dispatch that causes deadlock
            eventBus.Publish(new PipelineExecutionCompleted(
                context.RequestId,
                context.Result,
                stopwatch.Elapsed,
                executedHandlers,
                terminatingHandler
            ));
            
            // If we have a result, publish the translation event
            if (context.Result != null)
            {
                eventBus.Publish(new MessageTranslatedEvent(effect.Message, context.Result));
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Pipeline execution failed");
            
            // Just publish event - avoid nested dispatch that causes deadlock
            eventBus.Publish(new TranslationErrorEvent(effect.Message, ex.Message));
        }
    }
    
    private async Task ExecutePipelineAsync(
        List<ITranslationPipelineHandler> handlers,
        TranslationContext context,
        List<string> executedHandlers)
    {
        // Build the chain from the end to the beginning
        var pipeline = () => Task.CompletedTask;
        
        foreach (var handler in handlers.AsEnumerable().Reverse())
        {
            var next = pipeline;
            var currentHandler = handler;
            
            pipeline = async () =>
            {
                if (context.IsHandled)
                {
                    // Pipeline already terminated by a previous handler
                    return;
                }
                
                var handlerStopwatch = Stopwatch.StartNew();
                
                try
                {
                    executedHandlers.Add(currentHandler.Name);
                    
                    await currentHandler.HandleAsync(context, next).ConfigureAwait(false);
                    
                    handlerStopwatch.Stop();
                    
                    // Log handler execution without dispatching to avoid deadlock
                    logger.Debug($"Handler {currentHandler.Name} executed in {handlerStopwatch.ElapsedMilliseconds}ms, Handled: {context.IsHandled}");
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Handler {Name} failed", currentHandler.Name);
                    throw new Exception($"Handler '{currentHandler.Name}' failed: {ex.Message}", ex);
                }
            };
        }
        
        // Execute the pipeline
        await pipeline();
    }
}

/// <summary>
/// Publishes handler registration events to the EventBus.
/// </summary>
public class PublishHandlerRegisteredEffectHandler(EventBus eventBus)
    : IEffectHandler<PublishHandlerRegisteredEffect>
{
    public Task HandleAsync(PublishHandlerRegisteredEffect effect, IStore store)
    {
        eventBus.Publish(new PipelineHandlerRegistered(effect.Handler));
        return Task.CompletedTask;
    }
}

/// <summary>
/// Publishes handler unregistration events to the EventBus.
/// </summary>
public class PublishHandlerUnregisteredEffectHandler(EventBus eventBus)
    : IEffectHandler<PublishHandlerUnregisteredEffect>
{
    public Task HandleAsync(PublishHandlerUnregisteredEffect effect, IStore store)
    {
        eventBus.Publish(new PipelineHandlerUnregistered(effect.Handler));
        return Task.CompletedTask;
    }
}

/// <summary>
/// Publishes pipeline started events to the EventBus.
/// </summary>
public class PublishPipelineStartedEffectHandler(EventBus eventBus)
    : IEffectHandler<PublishPipelineStartedEffect>
{
    public Task HandleAsync(PublishPipelineStartedEffect effect, IStore store)
    {
        eventBus.Publish(new PipelineExecutionStarted(effect.RequestId, effect.Message));
        return Task.CompletedTask;
    }
}

/// <summary>
/// Publishes pipeline completion events to the EventBus.
/// </summary>
public class PublishPipelineCompletedEffectHandler(EventBus eventBus)
    : IEffectHandler<PublishPipelineCompletedEffect>
{
    public Task HandleAsync(PublishPipelineCompletedEffect effect, IStore store)
    {
        var terminatingHandler = !effect.ExecutedHandlers.IsEmpty ? effect.ExecutedHandlers.Last() : null;
        
        eventBus.Publish(new PipelineExecutionCompleted(
            effect.RequestId,
            effect.Result,
            effect.ExecutionTime,
            effect.ExecutedHandlers,
            terminatingHandler
        ));
        return Task.CompletedTask;
    }
}

/// <summary>
/// Publishes successfully translated message events to the EventBus.
/// </summary>
public class PublishTranslatedMessageEffectHandler(EventBus eventBus)
    : IEffectHandler<PublishTranslatedMessageEffect>
{
    public Task HandleAsync(PublishTranslatedMessageEffect effect, IStore store)
    {
        eventBus.Publish(new MessageTranslatedEvent(effect.OriginalMessage, effect.Result));
        return Task.CompletedTask;
    }
}

/// <summary>
/// Publishes translation error events to the EventBus.
/// </summary>
public class PublishTranslationErrorEffectHandler(EventBus eventBus)
    : IEffectHandler<PublishTranslationErrorEffect>
{
    public Task HandleAsync(PublishTranslationErrorEffect effect, IStore store)
    {
        eventBus.Publish(new TranslationErrorEvent(effect.OriginalMessage, effect.Error));
        return Task.CompletedTask;
    }
}
