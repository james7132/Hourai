using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Threading.Tasks;

namespace Hourai.Model {

//[Table("counters")]
//public class Counter {

  //public ushort Id { get; set; }
  //public string Name { get; set; }

  //public List<CounterEvent> Events { get; set; }

//}

//[Table("counter_events")]
//public class CounterEvent {

  //[Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
  //public ushort CounterId { get; set; }
  //[Required, ForeignKey("CounterId")]
  //public Counter Counter { get; set; }
  //[Required]
  //public long Timestamp { get; set; }
  //public DateTimeOffset Time {
    //get => DateTimeOffset.FromUnixTimeSeconds(Timestamp);
    //set => Timestamp = value.ToUnixTimeSeconds();
  //}
  //public long Value { get; set; }

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
