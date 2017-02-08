using Discord;
using Discord.Commands;
using RedditSharp;
using System.Threading.Tasks;
using Hourai.Preconditions;
using Hourai.Feeds.Services;

namespace Hourai.Modules {

public partial class Feeds {

  [Group("reddit")]
  public class RedditModule : HouraiModule {

    Reddit Reddit { get; }

    public RedditModule(RedditService redditService) {
      Reddit = redditService.Reddit;
    }

    [Command("add")]
    [Alias("+")]
    [RequirePermission(GuildPermission.ManageGuild)]
    public async Task Add(string subreddit) {
      var sub = await Reddit.GetSubredditAsync("/r/" + subreddit);
      if (sub == null)
        await RespondAsync($"Subreddit /r/{subreddit} does not exist");
      else
        await RespondAsync($"Subreddit /r/{subreddit} added");
    }

    //[Command("remove")]
    //[Alias("-")]
    //[RequirePermission(GuildPermission.ManageGuild)]
    //public async Task Remove(string subreddit) {
    //}

    //[Command("list")]
    //[RequirePermission(GuildPermission.ManageGuild)]
    //public async Task List() {
    //}

  }
}


}
