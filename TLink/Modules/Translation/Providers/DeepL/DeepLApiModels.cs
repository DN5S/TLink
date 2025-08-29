using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TLink.Modules.Translation.Providers.DeepL;

public class DeepLTranslateRequest
{
    [JsonPropertyName("text")]
    public List<string> Text { get; set; } = [];

    [JsonPropertyName("source_lang")]
    public string? SourceLang { get; set; }

    [JsonPropertyName("target_lang")]
    public string TargetLang { get; set; } = "EN";

    [JsonPropertyName("preserve_formatting")]
    public bool? PreserveFormatting { get; set; }

    [JsonPropertyName("tag_handling")]
    public string? TagHandling { get; set; }

    [JsonPropertyName("split_sentences")]
    public string? SplitSentences { get; set; }

    [JsonPropertyName("ignore_tags")]
    public List<string>? IgnoreTags { get; set; }
}

public class DeepLTranslateResponse
{
    [JsonPropertyName("translations")]
    public List<DeepLTranslation> Translations { get; set; } = [];
}

public class DeepLTranslation
{
    [JsonPropertyName("detected_source_language")]
    public string? DetectedSourceLanguage { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

public class DeepLLanguage
{
    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("supports_formality")]
    public bool? SupportsFormality { get; set; }
}

public class DeepLUsage
{
    [JsonPropertyName("character_count")]
    public long CharacterCount { get; set; }

    [JsonPropertyName("character_limit")]
    public long CharacterLimit { get; set; }
}

public class DeepLErrorResponse
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}