using System;
using System.Threading.Tasks;
using TLink.Core.MVU;
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
