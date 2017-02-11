using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Hourai.Model;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Hourai {

public class BotCommandService : IService {

  public DiscordShardedClient Client { get; set; }
  public CommandService Commands { get; set; }
  public DatabaseService Database { get; set; }
  public ErrorService ErrorService { get; set; }
  public CounterSet Counters { get; set; }
  public IDependencyMap Map { get; set; }

  public BotCommandService(DiscordShardedClient client, CommandService command) {
    Commands = command;
    Client = client;
    Client.MessageReceived += HandleMessage;
    if (Commands != null) {
      Log.Info("Loaded Modules: " + Commands.Modules.Select(c => c.Aliases.First()).Join(", "));
      Log.Info("Available Commands: " + Commands.Commands.Select(c => c.Aliases.First()).Join(", "));
    }
  }

  public async Task HandleMessage(IMessage m) {
    var msg = m as IUserMessage;
    if (msg == null || msg.Author.IsBot || msg.Author.IsMe())
      return;
    using (var db = Database.CreateContext()) {
      var user = db.GetUser(msg.Author);
      if(user.IsBlacklisted)
        return;

      // Marks where the command begins
      var argPos = 0;

      var guild = (m.Channel as IGuildChannel)?.Guild;
      char prefix;
      if(guild == null) {
        prefix = Config.CommandPrefix;
      } else {
        var dbGuild = db.GetGuild(guild);
        var prefixString = dbGuild.Prefix;
        if(string.IsNullOrEmpty(prefixString)) {
          prefix = Config.CommandPrefix;
          dbGuild.Prefix = prefix.ToString();
          await db.Save();
        } else {
          prefix = prefixString[0];
        }
      }

      // Determine if the msg is a command, based on if it starts with the defined command prefix
      if (!msg.HasCharPrefix(prefix, ref argPos))
        return;

      if (!msg.Channel.AllowCommands()) {
        Log.Info($"Attempted to run a command that is not allowed. {msg.Content.DoubleQuote()}");
        return;
      }

      // Execute the command. (result does not indicate a return value,
      // rather an object stating if the command executed succesfully)
      var context = new CommandContext(Client, msg);

      using(context.Channel.EnterTypingState()) {
        var result = await Commands.ExecuteAsync(context, argPos, Map);
        var guildChannel = msg.Channel as ITextChannel;
        string channelMsg = guildChannel != null ? $"in {guildChannel.Name} on {guildChannel.Guild.ToIDString()}."
          : "in private channel.";
        if (result.IsSuccess) {
          Log.Info($"Command successfully executed {msg.Content.DoubleQuote()} {channelMsg}");
          Counters.Get("command-success").Increment();
          return;
        }
        if (await CustomCommandCheck(msg, argPos, db))
          return;
        Log.Error($"Command failed {msg.Content.DoubleQuote()} {channelMsg} ({result.Error})");
        Counters.Get("command-failed").Increment();
        switch (result.Error) {
          // Ignore these kinds of errors, no need for response.
          case CommandError.UnknownCommand:
            return;
          default:
            if(result is ExecuteResult) {
              var executeResult = (ExecuteResult) result;
              ErrorService.RegisterException(executeResult.Exception);
              Log.Error(executeResult.Exception);
            } else {
              Log.Error(result.ErrorReason);
            }
            await msg.Respond(result.ErrorReason);
            break;
        }
      }
    }
  }

  async Task<bool> CustomCommandCheck(IMessage msg, int argPos, BotDbContext context) {
    var customCommandCheck = msg.Content.Substring(argPos).SplitWhitespace();
    if (customCommandCheck.Length <= 0)
      return false;
    var commandName = customCommandCheck[0];
    argPos += commandName.Length;
    var guild = (msg.Channel as ITextChannel)?.Guild;
    if(guild == null)
      return false;
    var command = context.Commands.FirstOrDefault(c => c.GuildId == guild.Id && c.Name == commandName);
    if (command == null)
      return false;
    await command.Execute(msg, msg.Content.Substring(argPos));
    Counters.Get("custom-command-executed").Increment();
    return true;
  }

}

}
