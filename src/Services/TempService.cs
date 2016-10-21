using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Hourai {

public class TempService {

  Bot Bot { get; }
  DiscordSocketClient Client { get; }
  BotDbContext Database { get; }

  public TempService(IDependencyMap map) {
    Bot = map.Get<Bot>();
    Database = map.Get<BotDbContext>();
    Client = map.Get<DiscordSocketClient>();
    Bot.RegularTasks += CheckTempActions;
  }

  async Task CheckTempActions() {
    var actions = Database.TempActions.OrderByDescending(b => b.End);
    var now = DateTimeOffset.Now;
    var done = new List<AbstractTempAction>();
    foreach(var action in actions) {
      if(action.End >= now)
        break;
      await action.Unapply(Client);
      done.Add(action);
    }
    if(done.Count > 0) {
      Database.TempActions.RemoveRange(done);
      await Database.Save();
    }
  }

}


}
