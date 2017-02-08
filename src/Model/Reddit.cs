using Discord;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace Hourai {

[Table("subreddits")]
public class Subreddit {

  [Key, DatabaseGenerated(DatabaseGeneratedOption.None), Required]
  public string Name { get; set; }

  public ICollection<SubredditChannel> Channels { get; set; }

  public async Task<IEnumerable<IMessageChannel>> GetChannels(IDiscordClient client) {
    Check.NotNull(client);
    var channels = new List<IMessageChannel>();
    foreach(var channel in Channels) {
      var discordChannel = await client.GetChannelAsync(channel.ChannelId) as IMessageChannel;
      if(discordChannel != null) {
        channels.Add(discordChannel);
      } else {
        Log.Error($"Channel {channel.ChannelId} for subreddit {Name} cannot be found");
      }
    }
    return channels;
  }

}

[Table("subreddit_channels")]
public class SubredditChannel {

  [Required]
  public string Name { get; set; }
  [Required]
  public ulong ChannelId { get; set; }
  [Required]
  public ulong GuildId { get; set; }

  [ForeignKey("Name")]
  public Subreddit Subreddit { get; set; }
  [ForeignKey("ChannelId")]
  public Channel Channel { get; set; }

}

}
