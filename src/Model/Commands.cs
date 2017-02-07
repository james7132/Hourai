using Discord;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Threading.Tasks;

namespace Hourai {

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

  internal struct Substring {
    public string String;
    public int Start;
    public int End;
    public Substring[] Elements;

    public int Length => End - Start;
    public override string ToString() =>
      String.Substring(Start, Length);
  }

  public CustomCommand() {
  }

  public Task Execute(IMessage message, string input) {
    var channel = Check.NotNull(message.Channel as ITextChannel);
    var builder = new StringBuilder(ResolveGroups(Response));
    return message.Respond(builder
      .Replace("$input", input.Trim())
      .Replace("$user", message.Author.Mention)
      .Replace("$channel", channel.Mention)
      .ToString());
  }

  static int MinPos(params int[] elements) {
    var min = int.MaxValue;
    foreach(var element in elements)
      if(element >= 0 && element < min)
        min = element;
    return (min != int.MaxValue) ? min : -1;
  }

  const char StartChar = '(';
  const char EndChar = ')';
  const char ElementChar = '|';

  static readonly Random rand = new Random();

  internal static string ResolveGroups(string text) {
    var changed = false;
    var builder = new StringBuilder(text);
    do {
      changed = false;
      foreach(var sub in FindGroups(builder.ToString()).OrderByDescending(s => s.End)) {
        var element = sub.Elements[rand.Next(sub.Elements.Length)];
        builder.Remove(sub.Start - 1, sub.Length + 2);
        builder.Insert(sub.Start - 1, element.ToString());
        changed = true;
      }
    } while(changed);
    return builder.ToString();
  }

  internal static IEnumerable<Substring> FindGroups(string text) {
    var stack = 0;
    var currentIndex = 0;
    var parenStart = 0;
    var index = 0;
    var elementStart = -1;
    var elements = new List<Substring>();
    Action addElement = () => elements.Add(new Substring {
              String = text,
              Start = elementStart,
              End = index,
              Elements = new Substring[] {}
            });
    while(currentIndex >= 0) {
      var start = text.IndexOf(StartChar, currentIndex);
      var end = text.IndexOf(EndChar, currentIndex);
      var element= text.IndexOf(ElementChar, currentIndex);
      index = MinPos(start, end, element);
      currentIndex = index;
      if(index < 0 || index >= text.Length)
        break;
      switch(text[index]) {
        case StartChar:
          if(stack == 0) {
            parenStart = index + 1;
            elementStart = index + 1;
          }
          stack++;
          break;
        case EndChar:
          if(stack == 1) {
            addElement();
            yield return new Substring {
              String = text,
              Start = parenStart,
              End = index,
              Elements = elements.ToArray()
            };
            elements.Clear();
          }
          stack = Math.Max(0, stack - 1);
          break;
        case ElementChar:
          if(stack == 1) {
            addElement();
            elementStart = index + 1;
          }
          break;
      }
      currentIndex++;
    }
  }

}

}
