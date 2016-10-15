using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Hourai {

public class DatabaseService {

  public BotDbContext Database { get; }
  public DiscordSocketClient Client { get; }

  public DatabaseService(BotDbContext db, DiscordSocketClient client) {
    Bot.RegularTasks += Bot.Database.Save;
    Client = client;
    Database = db;
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
