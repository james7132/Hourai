using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Hourai {

public class TempService {

  Bot Bot { get; }
  DiscordShardedClient Client { get; }

  public TempService(IDependencyMap map) {
    Bot = map.Get<Bot>();
    Client = map.Get<DiscordShardedClient>();
    Bot.RegularTasks += CheckTempActions;
  }

  async Task CheckTempActions() {
    using (var context = new BotDbContext()) {
      var actions = context.TempActions.OrderByDescending(b => b.End);
      var now = DateTimeOffset.Now;
      var done = new List<AbstractTempAction>();
      foreach(var action in actions) {
        if(action.End >= now)
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
