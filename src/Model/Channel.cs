using Discord;
using Discord.WebSocket;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Threading.Tasks;

namespace Hourai {

[Table("channels")]
public class Channel {

  [DatabaseGenerated(DatabaseGeneratedOption.None)]
  public ulong Id { get; set; }
  public ulong GuildId { get; set; }
  [Required]
  public Guild Guild { get; set; } 
  public bool SearchIgnored { get; set; }
  public bool JoinMessage { get; set; } 
  public bool LeaveMessage { get; set; } 
  public bool BanMessage { get; set; } 

  //public ICollection<SubredditChannel> Subreddits { get; set; } 

  public Channel() {
  }

  public Channel(IGuildChannel channel) {
    Id = Check.NotNull(channel).Id;
    GuildId = channel.Guild.Id;
  }

  public SocketChannel GetDiscordChannel() {
    return Bot.Client.GetChannel(Id);
  }

}

}
