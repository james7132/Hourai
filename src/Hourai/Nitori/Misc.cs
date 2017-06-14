using Discord.Commands;
using Hourai.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Net;

namespace Hourai.Nitori {

  public class Misc : HouraiModule {

    static IEnumerable<string> _lennyFaces =
      new [] {
        "( ͡° ͜ʖ ͡°)",
        "( ͠° ͟ʖ ͡°)",
        "ᕦ( ͡° ͜ʖ ͡°)ᕤ",
        "( ͡~ ͜ʖ ͡°)",
        "(ง ͠° ͟ل͜ ͡°)ง"
      };

    static IEnumerable<string> _eightBallResults =
      new [] {
        "It is certain.",
        "It is decidedly so.",
        "Without a doubt.",
        "Yes, definitely.",
        "You may rely on it.",
        "As I see it, yes.",
        "Most likely.",
        "Outlook good.",
        "Yes.",
        "Signs point to yes.",
        "Reply hazy try again...",
        "Ask again later...",
        "Better not tell you now...",
        "Cannot predict now...",
        "Concentrate and ask again...",
        "Don't count on it.",
        "My reply is no.",
        "My sources say no.",
        "Outlook not so good.",
        "Very doubtful.",
        "Why not?"
      };

    Regex Dice = new Regex(@"(\d+)d(\d+)(.?)(\d*)",
        RegexOptions.IgnoreCase |
        RegexOptions.Compiled);

    Regex fDice = new Regex(@"(\d+)df(.?)(\d*)",
        RegexOptions.IgnoreCase |
        RegexOptions.Compiled);

    [Command("blah")]
    public Task Blah() =>
      RespondAsync($"Blah to you too, {Context.User.Mention}");

    [Command("roll")]
    public async Task Roll([Remainder] string dice) {
      var userMention = Context.User.Mention;
      if (Dice.IsMatch(dice)) {
        Match m = Dice.Match(dice);
        int count = Convert.ToInt32(m.Groups[1].Value), sides = Convert.ToInt32(m.Groups[2].Value), modifier = 0;
        int.TryParse(m.Groups[4].Value, out modifier);
        if (count > 50) {
          await RespondAsync("You cannot roll more than 50 dice!");
          return;
        } else if (count < 1) {
          await RespondAsync($"{userMention} you rolled some invisible dice for a total of {RandomUtil.Int(-100, 0)}");
          return;
        }

        if (sides < 2) {
          await RespondAsync($"{userMention} that doesn't even make sense...");
          return;
        } else if (sides > 100) {
          await RespondAsync($"The limit is set to 100 sided dice.");
          return;
        }

        if (modifier > int.MaxValue / 2) {
          await RespondAsync("Ha, no.");
          return;
        }

        var rolls = new List<int>();

        for (int i = 0; i < count; i++)
          rolls.Add(RandomUtil.Int(1, sides + 1));

        double total = rolls.Sum();

        switch (m.Groups[3].Value.ToLower()) {
          case "+":
            total += modifier;
            break;
          case "-":
            total -= modifier;
            break;
          case "/":
            total /= modifier;
            break;
          case "*":
          case "x":
            total *= modifier;
            break;
          case "^":
            total = Math.Pow(total, modifier);
            break;
          case "!":
            int repeats = 0;

            var oldRolls = rolls.Where(x => x < modifier).ToList();

                foreach(var d in oldRolls) {
                    repeats++;

                    if (repeats > 20) {
                        await RespondAsync("That caused more than 20 dice to be rerolled, stopping now to prevent an infinite loop.");
                        return;
                    }

                    rolls.Add(RandomUtil.Int(1, sides + 1));
                }
                break;
            default:
                modifier = 0;
                break;
        }

        await RespondAsync($"{userMention} you rolled `{string.Join(", ", rolls.OrderByDescending(x => x))}`" + ((modifier > 0) ? $"{m.Groups[3].Value}`{modifier}`" : "") + $" for a total of `{total}`.");
    } else if (fDice.IsMatch(dice)) {
        Match m = fDice.Match(dice);
        int count = Convert.ToInt32(m.Groups[1].Value), modifier = 0;

        int.TryParse(m.Groups[3].Value, out modifier);

        if (count > 50) {
            await RespondAsync("You cannot roll more than 50 dice!");
            return;
        } else if (count < 1) {
            await RespondAsync($"{userMention} you rolled some invisible dice for a total of {RandomUtil.Int(-100, 0)}");
            return;
        }

        if (modifier > int.MaxValue / 2) {
            await RespondAsync("Ha, no.");
            return;
        }

        List<int> rolls = new List<int>();

        for (int i = 0; i < count; i++)
          rolls.Add(RandomUtil.Int(-2, 2));

        double total = rolls.Sum();

        switch (m.Groups[2].Value.ToLower()) {
            case "+":
                total += modifier;
                break;
            case "-":
                total -= modifier;
                break;
            case "/":
                total /= modifier;
                break;
            case "*":
            case "x":
                total *= modifier;
                break;
            case "^":
                total = Math.Pow(total, modifier);
                break;
            default:
                modifier = 0;
                break;
        }

        await RespondAsync($"{userMention} you rolled `{string.Join(", ", rolls.OrderByDescending(x => x).Select(x => x.ToString("+#;-#;0")))}`" + ((modifier > 0) ? $"{m.Groups[2].Value}`{modifier}`" : "") + $"  for a total of `{total}`.");
      } else {
        int max;
        if (int.TryParse(dice, out max)) {
          if (max > 0) {
            if (max <= 10000)
              await RespondAsync($"{userMention} you rolled {RandomUtil.Int(1, max)}.");
            else
              await RespondAsync($"{userMention} try a smaller number.");
          } else
            await RespondAsync($"{userMention} pick a number above 0");
        } else
          await RespondAsync("That's not a number or a dice roll :|");
      }
    }

    [Command("lmgtfy")]
    [Alias("goog", "lmg")]
    public Task LetMeGoogleThatForYou([Remainder] string query) =>
      RespondAsync($"https://lmgtfy.com/?q={WebUtility.UrlEncode(query)}");

    [Command("shrug")]
    [Remarks(@"```¯\\\_(ツ)_/¯```")]
    public Task Shrug() => RespondAsync(@"¯\\\_(ツ)_/¯");

    [Command("lenny")]
    [Remarks("You know what this is")]
    public Task Lenny() => RespondAsync(_lennyFaces.SelectRandom());

  }

}
