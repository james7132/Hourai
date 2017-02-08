using Discord.Commands;
using RedditSharp;
using System.Linq;
using System.Threading.Tasks;

namespace Hourai.Feeds.Services {

public class RedditService {

  BotWebAgent Agent { get; }
  public Reddit Reddit { get; }

  public RedditService() {
    Agent = new BotWebAgent(Config.RedditUsername,
        Config.RedditPassword,
        Config.RedditClientID,
        Config.RedditClientSecret,
        Config.RedditRedirectUri);
    Reddit = new Reddit(Agent, false);
    Bot.RegularTasks += PrintRTouhou;
  }

  async Task PrintRTouhou() {
    var sub = await Reddit.GetSubredditAsync("/r/touhou");
    foreach(var post in sub.New.Take(25)) {
      Log.Debug(post.Title);
    }
  }

}

}
