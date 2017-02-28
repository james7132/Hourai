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
      foreach(var module in Commands.Modules) {
        Log.Info("Loaded module: " + module.Name);
        foreach (var cmd in module.Commands) {
          Log.Info("Command: " + cmd.GetFullName());
        }
      }
    }
  }

  public async Task HandleMessage(IMessage m) {
    var msg = m as SocketUserMessage;
    if (msg == null || msg.Author.IsBot || msg.Author.IsMe())
      return;
    using (var db = Database.CreateContext()) {
      var user = db.Users.Get(msg.Author);
      if(user.IsBlacklisted)
        return;

      // Marks where the command begins
      var argPos = 0;

      Guild dbGuild = null;
      char prefix = Config.CommandPrefix;
      var guild = (m.Channel as IGuildChannel)?.Guild;
      if(guild != null) {
        dbGuild = db.Guilds.Get(guild);
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

      // Execute the command. (result does not indicate a return value,
      // rather an object stating if the command executed succesfully)
      var context = new HouraiCommandContext(Client, msg, db, user, dbGuild);

      if (Commands.Search(context, argPos).IsSuccess) {
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
          switch (result.Error) {
            // Ignore these kinds of errors, no need for response.
            case CommandError.UnknownCommand:
              return;
            default:
              Log.Error($"Command failed {msg.Content.DoubleQuote()} {channelMsg} ({result.Error})");
              Counters.Get("command-failed").Increment();
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
      } else if (await CustomCommandCheck(context, argPos, db))
        return;
    }
  }

  async Task<bool> CustomCommandCheck(ICommandContext msg, int argPos, BotDbContext context) {
    var customCommandCheck = msg.Message.Content.Substring(argPos).SplitWhitespace();
    if (customCommandCheck.Length <= 0)
      return false;
    var commandName = customCommandCheck[0];
    argPos += commandName.Length;
    if (msg.Guild == null)
      return false;
    var command = context.Commands.Find(msg.Guild.Id, commandName);
    if (command == null)
      return false;
    await command.Execute(msg.Message, msg.Message.Content.Substring(argPos));
    Counters.Get("custom-command-executed").Increment();
    return true;
  }

}

}
