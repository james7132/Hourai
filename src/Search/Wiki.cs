using Discord;
using Discord.Commands;
using Hourai.Preconditions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hourai.Search {

public partial class Search {

  [Group("wiki")]
  public class Wiki : HouraiModule {

    public WikiService Service { get; set; }

    [Command]
    [ChannelRateLimit(2, 1)]
    public async Task Search([Remainder] string query) {
      //TODO(james7132): generalize this
      var results = await Service.SearchAsync("https://en.touhouwiki.net", query);
      var resultCount = results.Count();
      if (resultCount <= 0) {
        await RespondAsync("No results found.");
        return;
      }
      if (resultCount == 1) {
        await RespondAsync(results.First().Url);
        return;
      }
      var builder = new StringBuilder();
      var count = 1;
      foreach(var result in results) {
        builder.AppendLine($"{count}. [{result.Name}]({result.Url})");
        count++;
      }
      await ReplyAsync("", embed: new EmbedBuilder().WithDescription(builder.ToString()));
    }

  }

}

}
