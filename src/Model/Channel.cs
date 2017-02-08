using Discord;
using Discord.WebSocket;
using System;
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
  public bool VoiceMessage { get; set; }

  //[Required]
  //public ICollection<CounterEvent> Events { get; set; }
  //
  [Required]
  public ICollection<SubredditChannel> Subreddits { get; set; }

  public Channel() {
  }

  public Channel(IGuildChannel channel) {
    Id = Check.NotNull(channel).Id;
    GuildId = channel.Guild.Id;
  }

}

//[Table("counters")]
//public class Counter {

  //public ulong Id { get; set; }
  //public string Name { get; set; }

  //public List<CounterEvent> Events { get; set; }

//}

//public class CounterEvent {

  //[Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
  //public ulong CounterId { get; set; }
  //[Required]
  //public Counter Counter { get; set; }
  //public DateTime Timestamp { get; set; }
  //public ulong Count { get; set; }

  //public ulong? ChannelId { get; set; }
  //public ulong? GuildId { get; set; }
  //public ulong? UserId { get; set; }

  //public Channel Channel { get; set; }
  //[ForeignKey("GuildId")]
  //public Guild Guild { get; set; }
  //[ForeignKey("UserId")]
  //public User User { get; set; }
  //public GuildUser GuildUser { get; set; }

//}

}
