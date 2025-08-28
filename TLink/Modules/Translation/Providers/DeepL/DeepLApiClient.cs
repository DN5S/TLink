using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;

namespace TLink.Modules.Translation.Providers.DeepL;

public class DeepLApiClient : IDisposable
{
    private readonly HttpClient httpClient;
    private readonly DeepLConfig config;
    private readonly IPluginLog logger;
    private readonly JsonSerializerOptions jsonOptions;
    
    public static readonly Dictionary<string, string> LanguageCodeMap = new()
    {
        ["auto"] = "",
        ["ja"] = "JA",
        ["en"] = "EN",
        ["de"] = "DE",
        ["fr"] = "FR",
        ["es"] = "ES",
        ["it"] = "IT",
        ["nl"] = "NL",
        ["pl"] = "PL",
        ["pt"] = "PT",
        ["ru"] = "RU",
        ["zh"] = "ZH",
        ["ko"] = "KO"
    };
    
    public DeepLApiClient(DeepLConfig config, IPluginLog logger)
    {
        this.config = config ?? throw new ArgumentNullException(nameof(config));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        httpClient = new HttpClient
        {
            BaseAddress = new Uri(config.GetApiUrl()),
            Timeout = TimeSpan.FromMilliseconds(config.TimeoutMs)
        };
        
        httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("DeepL-Auth-Key", config.ApiKey);
        httpClient.DefaultRequestHeaders.Add("User-Agent", "TLink/1.0");
        
        jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }
    
    public async Task<string> TranslateTextAsync(
        string text, 
        string sourceLang, 
        string targetLang,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;
        
        var request = new DeepLTranslateRequest
        {
            Text = [text],
            SourceLang = MapLanguageCode(sourceLang, isSource: true),
            TargetLang = MapLanguageCode(targetLang, isSource: false),
            PreserveFormatting = config.PreserveFormatting ? 1 : 0
        };
        
        if (config.PreserveFormatting)
        {
            request.TagHandling = "xml";
            request.SplitSentences = "0";
        }
        
        var content = new StringContent(
            JsonSerializer.Serialize(request, jsonOptions),
            Encoding.UTF8,
            "application/json"
        );
        
        int retryCount = 0;
        while (retryCount <= config.MaxRetries)
        {
            try
            {
                var response = await httpClient.PostAsync(
                    "translate",
                    content,
                    cancellationToken
                );
                
                var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<DeepLTranslateResponse>(
                        responseText,
                        jsonOptions
                    );
                    
                    return result?.Translations.FirstOrDefault()?.Text ?? text;
                }
                
                if ((int)response.StatusCode >= 500 && retryCount < config.MaxRetries)
                {
                    retryCount++;
                    await Task.Delay(1000 * retryCount, cancellationToken);
                    continue;
                }
                
                var error = JsonSerializer.Deserialize<DeepLErrorResponse>(
                    responseText,
                    jsonOptions
                );
                
                logger.Error($"DeepL API error ({response.StatusCode}): {error?.Message ?? responseText}");
                throw new HttpRequestException($"DeepL API error: {error?.Message ?? response.ReasonPhrase}");
            }
            catch (TaskCanceledException)
            {
                logger.Warning("DeepL API request timed out");
                throw;
            }
            catch (HttpRequestException ex) when (retryCount < config.MaxRetries)
            {
                retryCount++;
                logger.Warning($"DeepL API request failed (attempt {retryCount}): {ex.Message}");
                await Task.Delay(1000 * retryCount, cancellationToken);
            }
        }
        
        throw new HttpRequestException($"DeepL API request failed after {config.MaxRetries} retries");
    }
    
    public async Task<List<DeepLLanguage>> GetSupportedLanguagesAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync("languages", cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<List<DeepLLanguage>>(responseText, jsonOptions) ?? [];
    }
    
    public async Task<DeepLUsage> GetUsageAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync("usage", cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<DeepLUsage>(responseText, jsonOptions) 
            ?? new DeepLUsage();
    }
    
    public async Task<bool> ValidateApiKeyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await GetUsageAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            logger.Warning($"DeepL API key validation failed: {ex.Message}");
            return false;
        }
    }
    
    private string MapLanguageCode(string code, bool isSource)
    {
        if (string.IsNullOrWhiteSpace(code))
            return isSource ? "" : "EN";
        
        var lowerCode = code.ToLowerInvariant();
        
        if (LanguageCodeMap.TryGetValue(lowerCode, out var mappedCode))
            return mappedCode;
        
        return code.ToUpperInvariant();
    }
    
    public void Dispose()
    {
        httpClient.Dispose();
        GC.SuppressFinalize(this);
    }
}
