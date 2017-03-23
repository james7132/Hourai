using Discord.Commands;
using Discord.WebSocket;
using Hourai.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Hourai.Admin {

public class TempService : IService {

  public DiscordShardedClient Client { get; set; }
  public ErrorService ErrorService { get; set; }

  public TempService() {
    Bot.RegularTasks += CheckTempActions;
  }

  async Task CheckTempActions() {
    using (var context = new BotDbContext()) {
      await Task.WhenAll(context.TempActions
          .Where(a => a.Expiration < DateTimeOffset.Now)
          .AsEnumerable()
          .Select(action =>
            Task.Run(async () => {
        try {
          if (action.Reverse)
            await action.Apply(Client);
          else
            await action.Unapply(Client);
          context.TempActions.Remove(action);
        } catch(Exception e) {
          Log.Error(e);
          ErrorService.RegisterException(e);
        }
      })));
      await context.Save();
    }
  }

}


}
