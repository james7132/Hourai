using Discord;
using Discord.WebSocket;
using Discord.Net;
using Discord.Commands;
using Hourai.Model;
using Hourai.Extensions;
using RedditSharp;
using RedditSharp.Things;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Subreddit = RedditSharp.Things.Subreddit;
using DbSubreddit = Hourai.Model.Subreddit;

namespace Hourai.Feeds {

public class RedditService : IService {

  public DiscordShardedClient Client { get; set; }
  BotWebAgent Agent { get; }
  public Reddit Reddit { get; }

  ConcurrentDictionary<string, RedditSharp.Things.Subreddit> Subreddits { get; }

  public RedditService() {
    WebAgent.UserAgent = $"ubuntu:discord.bot.hourai:{Config.Version}";
    Agent = new BotWebAgent(Config.RedditUsername,
        Config.RedditPassword,
        Config.RedditClientID,
        Config.RedditClientSecret,
        Config.RedditRedirectUri);
    Reddit = new Reddit(Agent, false);
    Subreddits = new ConcurrentDictionary<string, Subreddit>();
    Bot.RegularTasks += CheckReddits;
  }

  Embed PostToMessage(Post post) {
    const int maxLength = 500;
    string description;
    if (post.IsSelfPost) {
      var selfText = post.SelfText;
      if (selfText.Length > maxLength) {
        description = selfText.Substring(0, maxLength) + "...";
      } else {
        description = selfText;
      }
    } else {
      description = post.Url.ToString();
    }
    return new EmbedBuilder {
        Title = post.Title,
        Url = "https://reddit.com" + post.Permalink.ToString(),
        Description = description,
        Timestamp = post.CreatedUTC,
        Author = new EmbedAuthorBuilder {
          Name = post.AuthorName,
          Url = "https://reddit.com/u/" + post.AuthorName
        }
      };
  }

  async Task CheckReddits() {
    Log.Info("CHECKING SUBREDDITS");
    using (var context = new BotDbContext()) {
      var subreddits = await context.Subreddits.Include(s => s.Channels).ToListAsync();
      await Task.WhenAll(subreddits.Select(async dbSubreddit => {
        Log.Info($"Checking {dbSubreddit.Name}");
        if (!dbSubreddit.Channels.Any()) {
          context.Subreddits.Remove(dbSubreddit);
          return;
        }
        var name = dbSubreddit.Name;
        RedditSharp.Things.Subreddit subreddit;
        if (!Subreddits.TryGetValue(name, out subreddit)) {
          subreddit = await Reddit.GetSubredditAsync(name);
          Subreddits[name] = subreddit;
        }
        var channels = await dbSubreddit.GetChannelsAsync(Client);
        if (!channels.Any())
          return;
        DateTimeOffset latest = dbSubreddit.LastPost ?? DateTimeOffset.UtcNow;
        var latestInPage = latest;
        var title = $"Post in /r/{dbSubreddit.Name}:";
        await subreddit.GetPosts(Subreddit.Sort.New).Take(25)
          .Where(p => p.CreatedUTC > latest)
          .OrderBy(p => p.CreatedUTC)
          .ForEachAwait(async post => {
              Log.Info($"New post in /r/{dbSubreddit.Name}: {post.Title}");
              var embed = PostToMessage(post);
              try {
                await Task.WhenAll(channels.Select(c => c.SendMessageAsync(title, false, embed)));
              } catch (Exception e) {
                Log.Error(e);
              }
              if (latestInPage < post.CreatedUTC) {
                latestInPage = post.CreatedUTC;
              }
            });
        if (latestInPage > latest)
          dbSubreddit.LastPost = latestInPage;
      }));
      Log.Info("Done checking subreddits");
      await context.Save();
    }
  }

}

}
