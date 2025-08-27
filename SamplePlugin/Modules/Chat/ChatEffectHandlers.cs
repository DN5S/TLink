using System;
using System.Threading.Tasks;
using SamplePlugin.Core.MVU;
using SamplePlugin.Core.Reactive;
using SamplePlugin.Modules.Chat.Models;

namespace SamplePlugin.Modules.Chat;

public class SaveConfigurationEffectHandler(Action<ChatModuleConfiguration> saveConfigAction)
    : IEffectHandler<SaveConfigurationEffect>
{
    public Task HandleAsync(SaveConfigurationEffect effect, IStore store)
    {
        saveConfigAction(effect.Configuration);
        return Task.CompletedTask;
    }
}

public class NotifyConfigurationChangedEffectHandler(EventBus eventBus)
    : IEffectHandler<NotifyConfigurationChangedEffect>
{
    public Task HandleAsync(NotifyConfigurationChangedEffect effect, IStore store)
    {
        eventBus.Publish(new ConfigurationUpdated(effect.Configuration));
        return Task.CompletedTask;
    }
}
