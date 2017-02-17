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
  [Remarks("Reddit related commands. Used to set up, configure, or remove reddit feeds.")]
  public class RedditModule : DatabaseHouraiModule {

    Reddit Reddit { get; }

    public RedditModule(RedditService redditService, DatabaseService db) : base(db) {
      Reddit = redditService.Reddit;
    }

    [Command("add")]
    [RequirePermission(GuildPermission.ManageGuild, Require.User | Require.BotOwnerOverride)]
    [Remarks("Adds subreddit feed(s) to the current channel. New posts on reddit will be automatically linked.")]
    public async Task Add(params string[] subreddits) {
      var channel = DbContext.GetChannel(Check.InGuild(Context.Message));
      var response = new StringBuilder();
      foreach(var sub in subreddits) {
        var subreddit = sub;
        var dbSubreddit = DbContext.FindSubreddit(sub);
        if (dbSubreddit == null) {
          var redditSubreddit = await Reddit.GetSubredditAsync("/r/" + sub);
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
    [Remarks("Removes a subreddit feed to the current channel.")]
    public async Task Remove(params string[] subreddits) {
      var channel = Check.InGuild(Context.Message);
      var builder = new StringBuilder();
      foreach(var subreddit in subreddits) {
        var name = DbContext.SanitizeSubredditName(subreddit);
        var dbSubreddit = DbContext.FindSubreddit(subreddit);
        var subChannel = DbContext.SubredditChannels.SingleOrDefault(s =>
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
        } else {
          builder.AppendLine($"Subreddit /r/{name} already does not post to this channel.");
        }
      }
      var results = builder.ToString();
      if (string.IsNullOrEmpty(results))
        await Success();
      else
        await RespondAsync(results);
    }

    [Command("list")]
    [Remarks("Lists all subreddits that feed into this channel.")]
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
