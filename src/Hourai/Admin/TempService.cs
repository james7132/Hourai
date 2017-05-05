using Discord.Commands;
using Discord.WebSocket;
using Hourai.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Hourai.Admin {

[Service]
public class TempService {

  readonly DiscordShardedClient _client;
  readonly ErrorService _errors;

  public TempService(DiscordShardedClient client,
                     ErrorService errors) {
    _client = client;
    _errors = errors;
    Bot.RegularTasks += CheckTempActions;
  }

  async Task CheckTempActions() {
    using (var context = new BotDbContext()) {
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
          Log.Error(e);
          _errors.RegisterException(e);
        }
      }));
      await context.Save();
    }
  }

}


}
