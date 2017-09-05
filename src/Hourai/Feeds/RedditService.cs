using Discord;
using Discord.WebSocket;
using Discord.Net;
using Discord.Commands;
using Hourai.Model;
using Hourai.Extensions;
using RedditSharp;
using RedditSharp.Things;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Subreddit = RedditSharp.Things.Subreddit;
using DbSubreddit = Hourai.Model.Subreddit;

namespace Hourai.Feeds {

[Service]
public class RedditService {

  public DiscordShardedClient Client { get; }
  public Reddit Reddit { get; }

  readonly IServiceProvider _services;
  readonly ILogger _log;
  ConcurrentDictionary<string, Subreddit> Subreddits { get; }

  public RedditService(IOptions<RedditConfig> redditConfig,
                       DiscordShardedClient client,
                       ILoggerFactory loggerFactory,
                       IServiceProvider services,
                       Bot bot) {
    Client = client;
    _services = services;
    var reddit = redditConfig.Value;
    var agent = new BotWebAgent(reddit.Username,
        reddit.Password,
        reddit.ClientID,
        reddit.ClientSecret,
        reddit.RedirectUri);
    agent.UserAgent = $"discord.bot.hourai:{bot.Version}";
    Reddit = new Reddit(agent, false);
    Subreddits = new ConcurrentDictionary<string, Subreddit>();
    Bot.RegularTasks += CheckReddits;
    _log = loggerFactory.CreateLogger<RedditService>();
  }

  async Task<Subreddit> GetSubredditAsync(string name) {
      Subreddit subreddit;
      if (!Subreddits.TryGetValue(name, out subreddit)) {
        _log.LogInformation($"Getting subreddit: {name}");
        subreddit = await Reddit.GetSubredditAsync(name);
        Subreddits[name] = subreddit;
        _log.LogInformation($"Got subreddit: {name}");
      }
      return subreddit;
  }

  Embed PostToMessage(Post post) {
    const int maxLength = 500;
    string description;
    if (post.IsSelfPost) {
      var selfText = post.SelfText;
      if (selfText.Length > maxLength) {
        description = selfText.Substring(0, maxLength) + "..."; } else {
        description = selfText;
      }
    } else {
      description = post.Url.ToString();
    }
    return new EmbedBuilder {
        Title = post.Title.Ellipisize(256),
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
    _log.LogInformation("CHECKING SUBREDDITS");
    using (var context = _services.GetService<BotDbContext>()) {
      var subreddits = await context.Subreddits.Include(s => s.Channels).ToListAsync();
      await Task.WhenAll(subreddits.Select(async dbSubreddit => {
        _log.LogInformation($"Checking {dbSubreddit.Name}");
        if (!dbSubreddit.Channels.Any()) {
          context.Subreddits.Remove(dbSubreddit);
          return;
        }
        var channels = dbSubreddit.GetChannels(Client);
        if (!channels.Any())
          return;
        var name = dbSubreddit.Name;
        var subreddit = await GetSubredditAsync(name);
        DateTimeOffset latest = dbSubreddit.LastPost ?? DateTimeOffset.UtcNow;
        var latestInPage = latest;
        var title = $"Post in /r/{dbSubreddit.Name}:";
        await subreddit.GetPosts(Subreddit.Sort.New, 25)
          .Where(p => p.CreatedUTC > latest)
          .OrderBy(p => p.CreatedUTC)
          .ForEachAwait(async post => {
              _log.LogInformation($"New post in /r/{dbSubreddit.Name}: {post.Title}");
              var embed = PostToMessage(post);
              try {
                await Task.WhenAll(channels.Select(c => c.SendMessageAsync(title, false, embed)));
              } catch (Exception e) {
                _log.LogError(0, e, "Reddit post broadcast failed.");
              }
              if (latestInPage < post.CreatedUTC) {
                latestInPage = post.CreatedUTC;
              }
            });
        if (latestInPage > latest)
          dbSubreddit.LastPost = latestInPage;
      }));
      _log.LogInformation("Done checking subreddits");
      await context.Save();
    }
  }

}

}
