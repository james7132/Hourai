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
    public class Search : HouraiModule {

      [Command("server")]
      public Task Server(string name) =>
        SearchBot(name, regex =>
             from guild in Context.Client.Guilds
             where regex.IsMatch(guild.Name)
             select guild.ToIDString().Code()
          );

      [Command("user")]
      public Task User(string username) =>
        SearchBot(username, regex =>
             from user in Db.Users
             where regex.IsMatch(user.Username)
             select $"{user.Username} ({user.Id})"
          );


      async Task SearchBot(string name, Func<Regex, IEnumerable<string>> resultFun) {
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
