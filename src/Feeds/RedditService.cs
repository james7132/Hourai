using Discord;
using Discord.WebSocket;
using Discord.Net;
using Discord.Commands;
using Hourai.Model;
using Hourai.Extensions;
using RedditSharp;
using RedditSharp.Things;
using System;
using System.Linq;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Hourai.Feeds {

public class RedditService : IService {

  public DiscordShardedClient Client { get; set; }
  BotWebAgent Agent { get; }
  public Reddit Reddit { get; }

  ConcurrentDictionary<string, RedditSharp.Things.Subreddit> Subreddits { get; }

  public RedditService() {
    Agent = new BotWebAgent(Config.RedditUsername,
        Config.RedditPassword,
        Config.RedditClientID,
        Config.RedditClientSecret,
        Config.RedditRedirectUri);
    Reddit = new Reddit(Agent, false);
    Subreddits = new ConcurrentDictionary<string, RedditSharp.Things.Subreddit>();
    Bot.RegularTasks += CheckReddits;
  }

  Embed PostToMessage(Post post) {
    var builder = new EmbedBuilder();
    builder.Title = post.Title;
    builder.Url = "https://reddit.com" + post.Permalink.ToString();
    var author = post.AuthorName;
    builder.Author = new EmbedAuthorBuilder() {
      Name = author,
      Url = "https://reddit.com/u/" + author }; if (!post.NSFW && !post.IsSelfPost) {
      builder.ThumbnailUrl = post.Thumbnail.ToString();
    }
    if (post.IsSelfPost) {
      var selfText = post.SelfText;
      const int maxLength = 500;
      if (selfText.Length > maxLength) {
        builder.Description = selfText.Substring(0, maxLength) + "...";
      } else {
        builder.Description = selfText;
      }
    } else {
      builder.Description = post.Url.ToString();
    }
    builder.Timestamp = post.CreatedUTC;
    return builder;
  }

  async Task CheckReddits() {
    using (var context = new BotDbContext()) {
      foreach (var dbSubreddit in context.Subreddits) {
        context.Entry(dbSubreddit).Collection(s => s.Channels).Load();
        if (!dbSubreddit.Channels.Any()) {
          context.Subreddits.Remove(dbSubreddit);
          continue;
        }
        var name = dbSubreddit.Name;
        RedditSharp.Things.Subreddit subreddit;
        if (!Subreddits.TryGetValue(name, out subreddit)) {
          subreddit = await Reddit.GetSubredditAsync("/r/" + name);
          Subreddits[name] = subreddit;
        }
        var channels = await dbSubreddit.GetChannelsAsync(Client);
        DateTimeOffset latest = dbSubreddit.LastPost ?? DateTimeOffset.UtcNow;
        var latestInPage = latest;
        await subreddit.New.Take(1).ForEachAwait(async page => {
              foreach(var post in page) {
                if (post.CreatedUTC <= latest)
                  break;
                var title = $"Post in /r/{dbSubreddit.Name}:";
                var embed = PostToMessage(post);
                foreach (var channel in channels) {
                  try {
                    await channel.SendMessageAsync(title, false, embed);
                  } catch (Exception e) {
                    Log.Error(e);
                  }
                }
                if (latestInPage < post.CreatedUTC) {
                  latestInPage = post.CreatedUTC;
                }
                await Task.Delay(500);
              }
            });
        dbSubreddit.LastPost = latestInPage;
        await context.Save();
      }
    }
  }

}

}
