using System;
using System.Threading.Tasks;
using TLink.Core.MVU;
using TLink.Core.Reactive;
using TLink.Modules.Chat.Models;

namespace TLink.Modules.Chat;

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
