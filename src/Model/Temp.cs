using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Hourai {

[Table("temp_bans")]
public class TempBan {
  [DatabaseGenerated(DatabaseGeneratedOption.None)]
  public ulong Id { get; set; }
  public ulong GuildId { get; set; }
  public DateTimeOffset Start { get; set; }
  public DateTimeOffset End { get; set; }

  [Required]
  [ForeignKey("Id")]
  public User User;

  [Required]
  [ForeignKey("GuildId")]
  public Guild Guild;
}

}
