using ModuleKit.MVU;

namespace TLink.Modules.Chat;

public record SaveConfigurationEffect : IEffect
{
    public string Type => "Chat/SaveConfiguration";
}