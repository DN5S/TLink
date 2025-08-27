using SamplePlugin.Core.MVU;

namespace SamplePlugin.Modules.Chat.Models;

public abstract record ChatEffect : IEffect
{
    public abstract string Type { get; }
}

public record SaveConfigurationEffect(ChatModuleConfiguration Configuration) : ChatEffect
{
    public override string Type => "Chat/SaveConfiguration";
}

public record NotifyConfigurationChangedEffect(ChatModuleConfiguration Configuration) : ChatEffect
{
    public override string Type => "Chat/NotifyConfigurationChanged";
}