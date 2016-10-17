using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Hourai {

public partial class Standard {

  [Group("search")]
  public class Search : DatabaseHouraiModule {

    LogSet Logs { get; }
    SearchService SearchService { get; }

    public Search(LogSet logs, BotDbContext db, SearchService searchService) : base(db) {
      Logs = logs;
      SearchService = searchService;
    }

    static Func<string, bool> ExactMatch(IEnumerable<string> matches) {
      return s => matches.All(s.Contains);
    }

    static Func<string, bool> RegexMatch(string regex) {
      return new Regex(regex, RegexOptions.Compiled).IsMatch;
    }

    [Command]
    [PublicOnly]
    [Remarks("Search the history of the current channel for messages that match all of the specfied search terms.")]
    public Task SearchChat(params string[] terms) {
      return SearchChannel(ExactMatch(terms));
    }

    [Command("regex")]
    [PublicOnly]
    [Remarks("Search the history of the current channel for matches to a specfied regex.")]
    public Task SearchRegex(string regex) {
      return SearchChannel(RegexMatch(regex));
    }

    [Command("day")]
    [PublicOnly]
    [Remarks("SearchChat the log of the the current channel on a certain day. Day must be of the format ``yyyy-mm-dd``")]
    public Task Day(string day) {
      var channel = Check.InGuild(Context.Message);
      string path = Logs.GetChannel(channel).GetPath(day);
      if (File.Exists(path))
        return Context.Message.SendFileRetry(path);
      else
        return RespondAsync($"A log for {channel.Mention} on date {day} cannot be found.");
    }

    [Command("ignore")]
    [PublicOnly]
    [Remarks("Mentioned channels will not be searched in ``search all``, except while in said channel. "
      + "User must have ``Manage Channels`` permission")]
    public Task Ignore(params IGuildChannel[] channels) => SetIgnore(channels, true);

    [Command("unigore")]
    [PublicOnly]
    [Remarks("Mentioned channels will appear in ``search all`` results." 
      +" User must have ``Manage Channels`` permission")]
    public Task Unignore(params IGuildChannel[] channels) => SetIgnore(channels, false);

    async Task SetIgnore(IEnumerable<IGuildChannel> channels, bool value) {
      var channel = Check.InGuild(Context.Message);
      foreach (var ch in channels) 
        (await Database.GetChannel(ch)).SearchIgnored = value;
      await Database.Save();
      await Success();
    }

    [Group("all")]
    public class All : HouraiModule {

      LogSet Logs { get; }
      SearchService SearchService { get; } 

      public All(LogSet logs, SearchService search) {
        Logs = logs;
        SearchService = search;
      }

      [Command]
      [PublicOnly]
      [Remarks("Searches the history of all channels in the current server for any of the specfied search terms.")]
      public Task SearchAll(params string[] terms) => SearchAll(ExactMatch(terms));

      [Command("regex")]
      [PublicOnly]
      [Remarks("Searches the history of all channels in the current server based on a regex.")]
      public async Task SearchAllRegex(string regex) => await SearchAll(RegexMatch(regex));

      async Task SearchAll(Func<string, bool> pred) {
        try {
          var channel = Check.InGuild(Context.Message);
          string reply = await SearchService.SearchAll(Context, pred);
          await Context.Message.Respond(reply);
        } catch (Exception e) {
          Log.Error(e);
        }
      }

    }

    async Task SearchChannel(Func<string, bool> pred) {
      try {
        var channel = Check.InGuild(Context.Message);
        string reply = await SearchService.Search(Context, pred);
        await Context.Message.Respond(reply);
      } catch(Exception e) {
        Log.Error(e);
      }
    }

  } 

}

}
