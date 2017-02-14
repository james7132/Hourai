using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Hourai.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Hourai {

  public partial class Owner {

    [Group("search")]
    public class Search : DatabaseHouraiModule {

      DiscordShardedClient Client { get; }

      public Search(DatabaseService db, DiscordShardedClient client) : base(db) {
        Client = client;
      }

      [Command("server")]
      public Task Server(string name) =>
        SearchDB(name, regex =>
             from guild in DbContext.Guilds.Select(g => Client.GetGuild(g.Id))
             where guild != null && regex.IsMatch(guild.Name)
             select guild.ToIDString().Code()
          );

      [Command("user")]
      public async Task User(string username) =>
        SearchDB(username, regex =>
             from user in DbContext.Users
             where regex.IsMatch(user.Username)
             select $"{user.Username} ({user.Id})"
          );


      async Task SearchDB(string name, Func<Regex, IEnumerable<string>> resultFun) {
        var regex = new Regex(name, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        var results = resultFun(regex);
        if (!results.Any())
          await RespondAsync("No matching results found");
        else
          await RespondAsync(results.Join("\n"));
      }

    }

  }

}
