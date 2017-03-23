using Discord;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Threading.Tasks;

namespace Hourai.Model {

[Table("commands")]
public class CustomCommand {

  [DatabaseGenerated(DatabaseGeneratedOption.None)]
  public ulong GuildId { get; set; }
  [ForeignKey("GuildId")]
  public Guild Guild { get; set; }
  [Required]
  public string Name { get; set; }
  [Required]
  public string Response { get; set; }

  public CustomCommand() {
  }

  public Task Execute(HouraiContext context, string input) =>
    context.Message.Respond(context.Process(Response));

}

}
