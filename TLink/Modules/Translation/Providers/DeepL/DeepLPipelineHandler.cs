using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using TLink.Modules.Translation.Models;
using TLink.Modules.Translation.Services;
using TLink.Utils;

namespace TLink.Modules.Translation.Providers.DeepL;

public class DeepLPipelineHandler(
    DeepLApiClient apiClient,
    DeepLConfig config,
    IPluginLog logger,
    SeStringProcessor seStringProcessor)
    : ITranslationPipelineHandler, IDisposable
{
    private readonly DeepLApiClient apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    private readonly DeepLConfig config = config ?? throw new ArgumentNullException(nameof(config));
    private readonly IPluginLog logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly SeStringProcessor seStringProcessor = seStringProcessor ?? throw new ArgumentNullException(nameof(seStringProcessor));
    
    public int Priority => 90;
    
    public string Name => "DeepL Translator";
    
    public bool IsEnabled => config.Enabled && config.IsConfigured();
    
    public IReadOnlyList<string> SupportedLanguages => DeepLApiClient.LanguageCodeMap.Keys.ToList();

    public async Task HandleAsync(TranslationContext context, Func<Task> next)
    {
        if (!IsEnabled)
        {
            await next().ConfigureAwait(false);
            return;
        }
        
        if (context.IsHandled)
        {
            return;
        }
        
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            logger.Debug($"DeepL: Starting translation for message from {context.OriginalMessage.Sender}");
            
            context.Metadata["DeepL.StartTime"] = DateTime.UtcNow;
            
            var encodedMessage = SeStringProcessor.Encode(context.OriginalMessage.SeStringMessage);
            
            if (string.IsNullOrWhiteSpace(encodedMessage.XmlText))
            {
                await next().ConfigureAwait(false);
                return;
            }
            
            var (translatedXml, detectedLanguage) = await apiClient.TranslateTextAsync(
                encodedMessage.XmlText,
                context.SourceLanguage,
                context.TargetLanguage
            ).ConfigureAwait(false);
            
            logger.Debug($"DeepL: Original XML: {encodedMessage.XmlText}");
            logger.Debug($"DeepL: Translated XML: {translatedXml}");
            
            var translatedSeString = seStringProcessor.Decode(translatedXml, encodedMessage.PayloadMap);
            logger.Debug($"DeepL: Decoded SeString payload count: {translatedSeString.Payloads.Count}");
            
            // Log the payload types for debugging
            var payloadTypes = translatedSeString.Payloads.Select(p => p.GetType().Name).ToList();
            logger.Debug($"DeepL: Payload types: {string.Join(", ", payloadTypes)}");
            
            stopwatch.Stop();
            
            var result = new TranslationResult(
                TranslatedText: translatedSeString.TextValue,
                TranslatedSeString: translatedSeString,
                DetectedLanguage: detectedLanguage,
                TranslatedBy: Name,
                FromCache: false,
                TranslationTime: stopwatch.Elapsed
            );
            
            context.SetResult(result);
            
            context.Metadata["DeepL.Success"] = true;
            context.Metadata["DeepL.TranslationTime"] = stopwatch.Elapsed;
            context.Metadata["DeepL.FormattingPreserved"] = true;
            
            logger.Debug($"DeepL: Translation completed in {stopwatch.ElapsedMilliseconds}ms");
        }
        catch (TaskCanceledException)
        {
            logger.Warning("DeepL: Translation request was cancelled or timed out");
            context.Metadata["DeepL.Error"] = "Timeout";
            await next().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.Error($"DeepL: Translation failed: {ex.Message}");
            context.Metadata["DeepL.Error"] = ex.Message;
            
            await next().ConfigureAwait(false);
        }
    }
    
    public void Dispose()
    {
        apiClient.Dispose();
        GC.SuppressFinalize(this);
    }
}
