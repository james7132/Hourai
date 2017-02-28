using Discord.Commands;
using Discord.WebSocket;
using Hourai.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Hourai.Admin {

public class TempService : IService {

  public DiscordShardedClient Client { get; }

  public TempService() {
    Bot.RegularTasks += CheckTempActions;
  }

  async Task CheckTempActions() {
    using (var context = new BotDbContext()) {
      var now = DateTimeOffset.Now;
      var done = new List<AbstractTempAction>();
      foreach(var action in context.TempActions) {
        if(action.Expiration >= now)
          break;
        await action.Unapply(Client);
        done.Add(action);
      }
      if(done.Count > 0) {
        context.TempActions.RemoveRange(done);
        await context.Save();
      }
    }
  }

}


}
