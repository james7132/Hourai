using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Hourai.Model;
using Hourai.Preconditions;

namespace Hourai.Standard {

/// <summary>
/// Generates a help method for all of a bot commands.
/// Cannot be automatically installed and must be installed after all other modules have been installed.
/// </summary>
public class Help : HouraiModule {

  public IDependencyMap Map { get; set; }
  public CommandService Commands { get; set; }

  const char CommandGroupChar = '*';

  [Command("help")]
  [UserRateLimit(1, 1)]
  public Task GetHelp([Remainder] string command = "") {
    Context.IsHelp = true;
    if(string.IsNullOrEmpty(command))
      return GeneralHelp();
    return SpecficHelp(command);
  }

  async Task<IEnumerable<ModuleInfo>> GetUsableModules(IEnumerable<ModuleInfo> modules)  {
    var usableModules = new List<ModuleInfo>();
    foreach(var module in modules) {
      var commands = await GetUsableCommands(module);
      if (commands.Any()) {
        usableModules.Add(module);
        continue;
      }
      var submodules = await GetUsableModules(module.Submodules);
      if (submodules.Any())
        usableModules.Add(module);
    }
    return usableModules;
  }

  async Task<string> CommandList(ModuleInfo module) {
    var commands = await GetUsableCommands(module);
    var modules = await GetUsableModules(module.Submodules);
    return commands.Select(c => c.GetFullName())
        .Concat(modules.Select(m => m.GetFullName() + "*"))
        .Select(n => n.Code())
        .Join(", ");
  }

  async Task GeneralHelp() {
    var builder = new StringBuilder();
    foreach(var module in Commands.Modules
        .Where(m => !m.IsSubmodule)
        .OrderBy(m => m.Name)) {
      var commands = await CommandList(module);
      if(string.IsNullOrEmpty(commands))
        continue;
      builder.AppendLine($"{module.Name.Bold()}: {commands}");
    }
    if(Context.Guild != null) {
      var guild = Db.GetGuild(Context.Guild);
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
    command = command.Trim();
    var searchResults = Commands.Search(Context, command);
    if(searchResults.IsSuccess) {
      await RespondAsync(await GetCommandInfo(searchResults.Commands.Select(c => c.Command)));
      return;
    }
    var module = SearchModules(command);
    if (module != null) {
      await RespondAsync(await ModuleHelp(module));
    } else {
      await RespondAsync(searchResults.ErrorReason).ConfigureAwait(false);
    }
  }

  ModuleInfo SearchModules(string command) {
    return Commands.Modules.FirstOrDefault(m => m.Aliases.Contains(command));
  }

  async Task<string> ModuleHelp(ModuleInfo module) {
    var builder = new StringBuilder();
    builder.AppendLine(module.GetFullName().Bold());
    if (!string.IsNullOrEmpty(module.Summary))
      builder.AppendLine(module.Summary);
    if (!string.IsNullOrEmpty(module.Remarks))
      builder.AppendLine(module.Remarks);
    if (module.Commands.Any() || module.Submodules.Any())
      builder.AppendLine("Commands: " + await CommandList(module));
    return builder.ToString();
  }

  async Task<List<CommandInfo>> GetUsableCommands(ModuleInfo module) {
    var usableCommands = new List<CommandInfo>();
    foreach(var command in module.Commands) {
      var result = await command.CheckPreconditionsAsync(Context, Map);
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
      var guild = Db.GetGuild(Context.Guild);
      var builder = new StringBuilder();
      var command = commands.First();
      using(builder.Code()) {
        builder.Append(guild.Prefix)
          .Append(command.GetFullName())
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
      var docPreconditions = command.Preconditions
        .OfType<DocumentedPreconditionAttribute>()
        .Select(d => d.GetDocumentation()).Join("\n");
      var mod = command.Module;
      while (mod != null) {
        var preconditions = mod.Preconditions
          .OfType<DocumentedPreconditionAttribute>()
          .Select(d => d.GetDocumentation()).Join("\n");
        if (!string.IsNullOrEmpty(preconditions))
          docPreconditions += "\n" + preconditions;
        mod = mod.Parent;
      }
      builder.AppendLine()
        .AppendLine(command.Remarks)
        .AppendLine(docPreconditions);
      // If it is a subgroup with a prefix, add all commands from that module to
      // the related commands
      var module = command.Module;
      commands = commands.Concat(await GetUsableCommands(module));
      var other = commands.Skip(1);
      if(other.Any()) {
        builder.Append("Related commands:")
          .AppendLine(other.Select(c => c.GetFullName().Code()).Distinct().Join(", "));
      }
      return builder.ToString();
    }
    return "No such command found";
  }

}

}
