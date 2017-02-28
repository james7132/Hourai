using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Threading.Tasks;

namespace Hourai.Model {

[Table("channels")]
public class Channel {

  [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
  public ulong Id { get; set; }
  public ulong? GuildId { get; set; }
  [ForeignKey("GuildId")]
  public Guild Guild { get; set; }

  public bool JoinMessage { get; set; }
  public bool LeaveMessage { get; set; }
  public bool BanMessage { get; set; }
  public bool VoiceMessage { get; set; }
  public bool StreamMessage { get; set; }

  //[Required]
  //public ICollection<CounterEvent> Events { get; set; }
  //
  [Required]
  public ICollection<SubredditChannel> Subreddits { get; set; }

  public Channel() {
  }

  public Channel(IChannel channel) {
    Id = Check.NotNull(channel).Id;
    GuildId = (channel as IGuildChannel)?.Guild.Id;
  }

}

}
