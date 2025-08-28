using System;
using System.Diagnostics;
using System.Threading.Tasks;
using TLink.Core.MVU;
using TLink.Core.Reactive;
using TLink.Modules.Translation.Models;
using TLink.Modules.Translation.Services;
using Dalamud.Plugin.Services;

namespace TLink.Modules.Translation.MVU;

public class TranslateRoutingEffectHandler(
    Func<ITranslationProvider?> getProvider,
    SeStringProcessor seStringProcessor,
    EventBus eventBus,
    IPluginLog logger)
    : IEffectHandler<RouteToProviderEffect>
{
    public async Task HandleAsync(RouteToProviderEffect effect, IStore store)
    {
        // NO ACTION DISPATCHING - Only publish events to EventBus
        
        var provider = getProvider();
        if (provider == null)
        {
            logger.Warning("No active translation provider for request {Id}", effect.Request.Id);
            // Publish failure event to EventBus
            eventBus.Publish(new ProviderTranslationFailed(effect.Request, "No active provider"));
            return;
        }
        
        try
        {
            var stopwatch = Stopwatch.StartNew();
            string translatedText;
            bool formattingPreserved;
            
            // Route based on provider capabilities
            if (provider.SupportsXmlTags)
            {
                // Provider supports XML - use SeString translation
                var encoded = seStringProcessor.Encode(effect.Request.Message.SeStringMessage);
                translatedText = await provider.TranslateAsync(
                    encoded.XmlText,
                    effect.Request.SourceLanguage,
                    effect.Request.TargetLanguage
                );
                // XML will be decoded by the display module
                formattingPreserved = true;
            }
            else
            {
                // Provider doesn't support XML - use plain text
                translatedText = await provider.TranslateAsync(
                    effect.Request.Message.Message,
                    effect.Request.SourceLanguage,
                    effect.Request.TargetLanguage
                );
                formattingPreserved = false;
            }
            
            stopwatch.Stop();
            
            var result = new TranslationResult(
                effect.Request.Message.Message,
                translatedText,
                formattingPreserved,
                stopwatch.Elapsed
            );
            
            // Publish success result via EventBus
            eventBus.Publish(new ProviderTranslationCompleted(effect.Request, result));
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Translation failed for provider {ProviderName} on request {Id}", 
                provider.Name, effect.Request.Id);
            // Publish failure event to EventBus
            eventBus.Publish(new ProviderTranslationFailed(effect.Request, ex.Message));
        }
    }
}

public class PublishCachedTranslationEffectHandler(EventBus eventBus) : IEffectHandler<PublishCachedTranslationEffect>
{
    public Task HandleAsync(PublishCachedTranslationEffect effect, IStore store)
    {
        // Publish cached result directly
        eventBus.Publish(new MessageTranslatedEvent(
            effect.OriginalMessage,
            effect.CachedResult
        ));
        
        return Task.CompletedTask;
    }
}
