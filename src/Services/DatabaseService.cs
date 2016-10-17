using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Hourai {

public class DatabaseService {

  BotDbContext Database { get; }
  DiscordSocketClient Client { get; }

  public DatabaseService(IDependencyMap map) {
    Client = map.Get<DiscordSocketClient>();
    Database = map.Get<BotDbContext>();
    Bot.RegularTasks += Database.Save;
    Client.MessageReceived += async m => {
      var author = m.Author;
      if(author.Username == null)
        return;
      var user = await Database.GetUser(author);
      user.AddName(author.Username);
      await Database.Save();
    };
  }

}

}
