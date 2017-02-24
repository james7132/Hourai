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
  public class RedditModule : HouraiModule {

    public RedditService Service { get; set; }
    Reddit Reddit => Service?.Reddit;

    [Command("add")]
    [RequirePermission(GuildPermission.ManageGuild, Require.User | Require.BotOwnerOverride)]
    [Remarks("Adds subreddit feed(s) to the current channel. New posts on reddit will be automatically linked.")]
    public async Task Add(params string[] subreddits) {
      var channel = Db.GetChannel(Check.InGuild(Context.Message));
      var response = new StringBuilder();
      foreach(var sub in subreddits) {
        var subreddit = Subreddit.SanitizeName(sub);
        var dbSubreddit = Db.Subreddits.Find(sub);
        if (dbSubreddit == null) {
          var redditSubreddit = await Reddit.GetSubredditAsync("/r/" + sub);
          if (redditSubreddit == null) {
            response.AppendLine($"Subreddit /r/{subreddit} does not exist");
            continue;
          }
          dbSubreddit = Db.GetSubreddit(sub);
        }
        subreddit = dbSubreddit.Name;
        if (dbSubreddit.Channels.Any(c => c.ChannelId == channel.Id)) {
          await RespondAsync($"Subreddit /r/{subreddit} already posts to this channel.");
        } else {
          dbSubreddit.Channels.Add(new SubredditChannel { Channel = channel, Subreddit = dbSubreddit });
          response.AppendLine($"Subreddit /r/{subreddit} added.");
        }
      }
      await Db.Save();
      await RespondAsync(response.ToString());
    }

    [Command("remove")]
    [RequirePermission(GuildPermission.ManageGuild, Require.User | Require.BotOwnerOverride)]
    [Remarks("Removes a subreddit feed to the current channel.")]
    public async Task Remove(params string[] subreddits) {
      var channel = Check.InGuild(Context.Message);
      var builder = new StringBuilder();
      foreach(var subreddit in subreddits) {
        var name = Subreddit.SanitizeName(subreddit);
        var dbSubreddit = Db.Subreddits.Find(name);
        var subChannel = Db.SubredditChannels.Find(name, channel.Id);
        if (subChannel != null){
          Db.SubredditChannels.Remove(subChannel);
          await Db.Save();
          if (dbSubreddit != null && dbSubreddit.Channels.Count <= 0) {
            Db.Subreddits.Remove(dbSubreddit);
            await Db.Save();
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
      var channel = Db.GetChannel(Check.InGuild(Context.Message));
      if (!channel.Subreddits.Any())
        await RespondAsync("No subreddits currently tied to this channel");
      else
        await RespondAsync(channel.Subreddits.Select(s => $"/r/{s.Name}".Code()).Join(", "));
    }

  }
}


}
