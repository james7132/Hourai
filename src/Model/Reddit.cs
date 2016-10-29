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

  //public IAsyncEnumerable<IMessageChannel> GetChannels(IDiscordClient client) {
    //Check.NotNull(client);
    //return AsyncEnum.Enumerate<IMessageChannel>(async consumer => {
          //foreach(var channel in Channels) {
            //var discordChannel = await client.GetChannelAsync(channel.Id) as IMessageChannel;
            //if(discordChannel != null)
              //await consumer.YieldAsync(discordChannel);
          //}
        //});
  //}

}

[Table("subreddit_channels")]
public class SubredditChannel {

  [Required]
  public string Name { get; set; }
  [Required]
  public ulong ChannelId { get; set; }

  [ForeignKey("Name")]
  public Subreddit Subreddit { get; set; }
  //[ForeignKey("Id")]
  //public Channel Channel { get; set; }

}

}
