using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Hourai {

public class DatabaseService {

  public BotDbContext Context { get; private set; }
  DiscordSocketClient Client { get; }

  public BotDbContext CreateContext() {
    return (Context = new BotDbContext());
  }

  public DatabaseService(IDependencyMap map) {
    Client = map.Get<DiscordSocketClient>();
    //Bot.RegularTasks += Database.Save;
    Client.MessageReceived += async m => {
      var author = m.Author;
      if(author.Username == null)
        return;
      using (var context = new BotDbContext()) {
        var user = context.GetUser(author);
        user.AddName(author.Username);
        await context.Save();
      }
    };
  }

}

}
