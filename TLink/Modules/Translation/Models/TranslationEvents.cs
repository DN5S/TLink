using TLink.Modules.Chat.Models;

namespace TLink.Modules.Translation.Models;

// Event published when a message has been translated
public record MessageTranslatedEvent(
    ChatMessage OriginalMessage,
    TranslationResult TranslatedResult
);

// Event published by provider modules when translation is complete
public record ProviderTranslationCompleted(
    TranslationRequest Request,
    TranslationResult Result
);

// Event published when a provider list changes
public record ProvidersUpdatedEvent(
    string[] AvailableProviders,
    string ActiveProvider
);

// Event published when the provider fails to translate
public record ProviderTranslationFailed(
    TranslationRequest Request,
    string Error
);

// Event published when translation fails
public record TranslationErrorEvent(
    ChatMessage OriginalMessage,
    string Error
);
