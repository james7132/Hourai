using Discord.Commands;
using Discord.WebSocket;
using Hourai.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Hourai.Admin {

[Service]
public class TempService {

  readonly DiscordShardedClient _client;
  readonly ErrorService _errors;
  readonly IServiceProvider _services;
  readonly ILogger _log;

  public TempService(DiscordShardedClient client,
                     ErrorService errors,
                     ILoggerFactory loggerFactory,
                     IServiceProvider services) {
    _client = client;
    _errors = errors;
    _log = loggerFactory.CreateLogger<TempService>();
    _services = services;
    Bot.RegularTasks += CheckTempActions;
  }

  async Task CheckTempActions() {
    using (var context = _services.GetService<BotDbContext>()) {
      await Task.WhenAll(context.TempActions
          .Where(a => a.Expiration < DateTimeOffset.Now)
          .AsEnumerable()
          .Select(async action => {
        try {
          if (action.Reverse)
            await action.Apply(_client);
          else
            await action.Unapply(_client);
          context.TempActions.Remove(action);
        } catch(Exception e) {
          _log.LogError(0, e, "Temp action execution failed.");
          _errors.RegisterException(e);
        }
      }));
      await context.Save();
    }
  }

}


}
