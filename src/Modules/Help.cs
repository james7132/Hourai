using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace Hourai {

/// <summary>
/// Generates a help method for all of a bot commands.
/// Cannot be automatically installed and must be installed after all other modules have been installed.
/// </summary>
[DontAutoLoad]
public class Help : HouraiModule {

  readonly Dictionary<ModuleInfo, CommandGroup> Modules;

  static Queue<string> SplitComamnd(string input) {
    return new Queue<string>(input.SplitWhitespace());
  }

  public class CommandGroup {

    public string Name{  get; }
    public CommandInfo Command { get; }
    public Dictionary<string, CommandGroup> Subcommands { get; }

    CommandGroup() {
      Subcommands = new Dictionary<string, CommandGroup>();
    }

    public CommandGroup(IEnumerable<CommandInfo> commands) : this() {
      Name = string.Empty;
      Command = null;
      foreach (CommandInfo command in commands.EmptyIfNull())
        AddCommand(command);
    }

    CommandGroup(string name) : this() {
      Name = name;
    }

    CommandGroup(CommandInfo command, string name) : this(name) {
      Name = name;
      Command = command;
    }

    void AddCommand(CommandInfo command) {
      AddCommand(SplitComamnd(command.Text), command);
    }

    void AddCommand(Queue<string> names, CommandInfo command) {
      Check.NotNull(names);
      if (names.Count == 0)
        return;
      var name = names.Dequeue();
      if (names.Count == 0) {
        if (Subcommands.ContainsKey(name))
          throw new Exception("Cannot register two commands to the same name");
        Subcommands.Add(name, new CommandGroup(command, name));
      } else {
        if(!Subcommands.ContainsKey(name))
          Subcommands[name] = new CommandGroup(name);
        Subcommands[name].AddCommand(names, command);
      }
    }

    public Task<string> GetSpecficHelp(CommandContext context, string command) {
      return GetSpecficHelp(context, SplitComamnd(command));
    }

    Task<string> GetSpecficHelp(CommandContext context, Queue<string> names) {
      if (names.Count == 0)
        return GetSpecficHelp(context);
      var prefix = names.Dequeue();
      // Recurse as needed
      if (Subcommands.ContainsKey(prefix))
        return Subcommands[prefix].GetSpecficHelp(context, names);
      return Task.FromResult<string>(null);
    }

    async Task<string> GetSpecficHelp(CommandContext context) {
      var builder = new StringBuilder();
      CommandInfo c = Command;
      using (builder.Code()) {
        builder.Append(Name);
        if(c != null) {
          foreach (CommandParameter parameter in c.Parameters) {
            var name = parameter.Name;
            if(parameter.IsMultiple || parameter.IsRemainder)
              name += "...";
            if (parameter.IsOptional || parameter.IsRemainder || parameter.IsMultiple)
              name = $"[{name}]";
            if (!name.IsNullOrEmpty())
              builder.Append(" ").Append(name);
          }
        }
      }
      builder.AppendLine();
      if (c != null)
        builder.AppendLine(c.Remarks);
      if(Subcommands.Count > 0) {
        builder.AppendLine("Available Subcommands:")
          .AppendLine(await ListSubcommands(context));
      }
      return builder.ToString();
    }

    public async Task<string> ListSubcommands(CommandContext context) {
      var usable = new List<string>();
      foreach (var commandGroup in Subcommands.Values) {
        if(await commandGroup.CanUse(context))
          usable.Add(commandGroup.ToString().Code());
      }
      return usable.Join(", ");
    }

    public async Task<bool> CanUse(CommandContext context) {
      if (Command != null) {
        var result = await Command.CheckPreconditions(context);
        if (result.IsSuccess)
          return true;
      }
      foreach (CommandGroup commandGroup in Subcommands.Values) {
        if (await commandGroup.CanUse(context))
          return true;
      }
      return false;
    }

    public override string ToString() {
      var str = Name.ToLowerInvariant();
      if (Subcommands.Count > 0)
        str += "*";
      return str;
    }
  }

  public Help() {
    var modules = Bot.CommandService?.Modules;
    if(modules == null)
      throw new InvalidOperationException("Cannot create a help command if there is no command service");
    Modules = modules.ToDictionary(g => g, g => new CommandGroup(g.Commands));
  }

  [Command("help")]
  [Remarks("Gets information about commands")]
  public async Task HelpCommand([Remainder] string command = "") {
    try {
      var message = Context.Message;
      if(command.IsNullOrEmpty()) {
        await RespondAsync(await GetGeneralHelp(Context));
        return;
      }
      foreach (CommandGroup commandGroup in Modules.Values) {
        string specificHelp = await commandGroup.GetSpecficHelp(Context, command);
        if (specificHelp.IsNullOrEmpty())
          continue;
        await RespondAsync(specificHelp);
        return;
      }
      await RespondAsync($"{message.Author.Mention}: No command named {command.DoubleQuote()} found");
    } catch(Exception e) {
        Log.Error(e + e.StackTrace);
    }
  }

  async Task<string> GetGeneralHelp(CommandContext context) {
    var builder = new StringBuilder();
    foreach (var kvp in Modules.OrderBy(k => k.Key.Name)) {
      if (!await kvp.Value.CanUse(context))
        continue;
      var name = kvp.Key.Name;
      if (!name.IsNullOrEmpty())
        builder.Append((name + ": ").Bold());
      builder.AppendLine(await kvp.Value.ListSubcommands(context));
    }
    var channel = context.Channel as ITextChannel;
    if(channel != null) {
      var guild = await Bot.Database.GetGuild(channel.Guild);
      var commands = guild.Commands;
      if(commands != null && commands.Count > 0)
        builder.AppendLine($"{"Custom: ".Bold()} {commands.Select(c => c.Name.Code()).Join(", ")}");
    }
    return $"{context.Message.Author.Mention} here are the commands you can use:\n{builder}\nRun ``help <command>`` for more information";
  }

}

}
