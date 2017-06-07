using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace Hourai.Model {

[Table("subreddits")]
public class Subreddit {

  private string _name;
  [Key, DatabaseGenerated(DatabaseGeneratedOption.None), Required]
  public string Name {
    get { return _name; }
    set { _name = SanitizeName(value); }
  }

  public DateTimeOffset? LastPost { get; set; }

  public ICollection<SubredditChannel> Channels { get; set; }

  public IEnumerable<ISocketMessageChannel> GetChannels(DiscordShardedClient client) {
    Check.NotNull(client);
    return Channels.Select(c => client.GetChannel(c.ChannelId) as ISocketMessageChannel)
        .Where(c => c != null);
  }

  public static string SanitizeName(string name) {
    return name.Trim().ToLower();
  }


}

[Table("subreddit_channels")]
public class SubredditChannel {

  [Required]
  public string Name { get; set; }
  [Required]
  public ulong ChannelId { get; set; }

  [ForeignKey("Name")]
  public Subreddit Subreddit { get; set; }
  [ForeignKey("ChannelId")]
  public Channel Channel { get; set; }

}

}
