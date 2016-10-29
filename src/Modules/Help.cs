using System;
using System.Collections.Generic; 
using System.Linq; 
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace Hourai { 

/// <summary>
/// Generates a help method for all of a bot commands.
/// Cannot be automatically installed and must be installed after all other modules have been installed.
/// </summary>
[Hide]
public class Help : DatabaseHouraiModule {

  IDependencyMap Map { get; } 
  CommandService Commands { get; }

  const char CommandGroupChar = '*';

  public Help(IDependencyMap map, CommandService commands, BotDbContext db) : base(db) { var modules = commands?.Modules;
    Map = map;
    Commands = commands;
  }

  [Command("help")]
  public Task GetHelp([Remainder] string command = "") {
    if(string.IsNullOrEmpty(command))
      return GeneralHelp();
    return SpecficHelp(command);
  }

  async Task GeneralHelp() {
    var builder = new StringBuilder();
    foreach(var module in Commands.Modules
        .Where(m => !m.Source.IsNested && 
          m.Source.GetCustomAttribute<HideAttribute>() == null)
        .OrderBy(m => m.Name)) {
      var commands = await GetUsableCommands(module);
      if(commands.Count <= 0)
        continue;
      var commandStrings = commands
        // Group by first prefix
        .GroupBy(c => c.Text.Split(null).First(), c => c)
        // Check if there is more than one with the same prefix.
        .Select(g => ((g.Skip(1).Any()) ? g.Key + CommandGroupChar : g.First().Text).Code())
        .Join(", ");
      builder.AppendLine($"{module.Name.Bold()}: {commandStrings}");
    }
    if(Context.Guild != null) {
      var guild = Database.GetGuild(Context.Guild);
      if(guild.Commands.Any())
        builder.AppendLine($"{"Custom".Bold()}: {guild.Commands.Select(c => c.Name.Code()).Join(", ")}");
    }
    var result = builder.ToString();
    if(!string.IsNullOrEmpty(result))
      await RespondAsync($"{Context.Message.Author.Mention}, here are the " +
          $"commands you can currently use\n{result}Use ``~help <command>`` for more information.");
    else
      await RespondAsync($"{Context.Message.Author.Mention}, there are no commands that you are allowed to use.");
  }

  async Task SpecficHelp(string command) {
    Log.Debug(command);
    command = command.Trim();
    var searchResults = Commands.Search(Context, command);
    if(searchResults.IsSuccess) {
      await RespondAsync(await GetCommandInfo(searchResults.Commands)).ConfigureAwait(false);
    } else {
      await RespondAsync(searchResults.ErrorReason).ConfigureAwait(false);
    }
  }

  async Task<List<CommandInfo>> GetUsableCommands(ModuleInfo module) {
    var usableCommands = new List<CommandInfo>();
    foreach(var command in module.Commands) {
      var result = await command.CheckPreconditions(Context, Map);
      if(result.IsSuccess)
        usableCommands.Add(command);
    }
    return usableCommands;
  }

  // Generates help description from a set of search results
  async Task<string> GetCommandInfo(IEnumerable<CommandInfo> commands) {
    // Reverse the commands. Order goes from least specific to most specfic.
    commands = commands.Reverse();
    if(commands.Any()) {
      var guild = Database.GetGuild(Context.Guild);
      var builder = new StringBuilder();
      var command = commands.First();
      using(builder.Code()) {
        builder.Append(guild.Prefix)
          .Append(command.Text)
          .Append(" ")
          .AppendLine(command.Parameters.Select(p => {
                var param = p.Name;
                var defaultString = p.DefaultValue as string;
                if(p.DefaultValue != null || !string.IsNullOrEmpty(defaultString))
                  param += $" = {p.DefaultValue}";
                if(p.IsRemainder || p.IsMultiple)
                  param += "...";
                if(p.IsOptional)
                  param = param.SquareBracket();
                else
                  param = param.AngleBracket();
                return param;
              }).Join(" "));
      }
      builder.AppendLine().AppendLine(command.Remarks);
      // If it is a subgroup with a prefix, add all commands from that module to
      // the related commands
      var module = command.Module;
      if(!string.IsNullOrEmpty(module.Prefix) && module.Source.IsNested)
        commands = commands.Concat(await GetUsableCommands(module));
      var other = commands.Skip(1);
      if(other.Any()) {
        builder.Append("Related commands:")
          .AppendLine(other.Select(c => c.Text.Code()).Distinct().Join(", "));
      }
      return builder.ToString();
    }
    return "No such command found";
  }

}

}
