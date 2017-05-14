using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Hourai.Model {

  [Table("quotes")]
  public class Quote {
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }
    public ulong GuildId { get; set; }
    [Required, ForeignKey("GuildId")]
    public Guild Guild { get; set; }
    [Required]
    public DateTimeOffset Created { get; set; }
    public bool Removed { get; set; }
    [Required]
    public string Author { get; set; }
    [Required]
    public ulong AuthorId { get; set; }
    [Required]
    public string Content { get; set; }
  }

}
