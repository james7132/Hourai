using Discord;
using Discord.Commands;
using Hourai.Model;
using Hourai.Preconditions;
using RedditSharp;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hourai.Feeds {

public partial class Feeds {

  [Group("reddit")]
  public class RedditModule : DatabaseHouraiModule {

    Reddit Reddit { get; }

    public RedditModule(RedditService redditService, DatabaseService db) : base(db) {
      Reddit = redditService.Reddit;
    }

    [Command("add")]
    [RequirePermission(GuildPermission.ManageGuild, Require.User | Require.BotOwnerOverride)]
    public async Task Add(string subreddit) {
      var channel = DbContext.GetChannel(Check.InGuild(Context.Message));
      var subreddits = subreddit.Split('+');
      var response = new StringBuilder();
      foreach(var sub in subreddits) {
        var dbSubreddit = DbContext.FindSubreddit(sub);
        if (dbSubreddit == null) {
          var redditSubreddit = await Reddit.GetSubredditAsync("/r/" + subreddit);
          if (redditSubreddit == null) {
            response.AppendLine($"Subreddit /r/{subreddit} does not exist");
            continue;
          }
          dbSubreddit = await DbContext.GetSubreddit(sub);
        }
        subreddit = dbSubreddit.Name;
        if (dbSubreddit.Channels.Any(c => c.ChannelId == channel.Id)) {
          await RespondAsync($"Subreddit /r/{subreddit} already posts to this channel.");
        } else {
          dbSubreddit.Channels.Add(new SubredditChannel { Channel = channel, Subreddit = dbSubreddit });
          response.AppendLine($"Subreddit /r/{subreddit} added.");
        }
      }
      await DbContext.Save();
      await RespondAsync(response.ToString());
    }

    [Command("remove")]
    [RequirePermission(GuildPermission.ManageGuild, Require.User | Require.BotOwnerOverride)]
    public async Task Remove(string subreddit) {
      var name = DbContext.SanitizeSubredditName(subreddit);
      var channel = Check.InGuild(Context.Message);
      var dbSubreddit = DbContext.FindSubreddit(subreddit);
      Log.Debug(dbSubreddit);
      var subChannel = DbContext.SubredditChannels.FirstOrDefault(s =>
          s.Name == name &&
          s.ChannelId == channel.Id &&
          s.GuildId == channel.Guild.Id);
      if (subChannel != null){
        DbContext.SubredditChannels.Remove(subChannel);
        await DbContext.Save();
        if (dbSubreddit != null && dbSubreddit.Channels.Count <= 0) {
          DbContext.Subreddits.Remove(dbSubreddit);
          await DbContext.Save();
        }
        await Success();
      } else {
        await RespondAsync($"Subreddit /r/{name} already does not post to this channel.");
      }
    }

    [Command("list")]
    public async Task List() {
      var channel = DbContext.GetChannel(Check.InGuild(Context.Message));
      if (!channel.Subreddits.Any())
        await RespondAsync("No subreddits currently tied to this channel");
      else
        await RespondAsync(channel.Subreddits.Select(s => $"/r/{s.Name}".Code()).Join(", "));
    }

  }
}


}
