using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Hourai {

public class BotCommandService : IService {

  public DiscordSocketClient Client { get; }
  public CommandService Commands { get; } 

  public BotCommandService(DiscordSocketClient client, CommandService command) {
    Client = client;
    Commands = command;
  }

  public async Task Initialize() {
    Client.MessageReceived += HandleMessage;
    var map = new DependencyMap();
    map.Add(Bot.Counters);
    Log.Info("HELLO");
    await Commands.AddModules(Assembly.GetEntryAssembly(), map);
    await Commands.AddModule<Help>(map);
    foreach(var module in Commands.Modules)
      Log.Info(module.Name);
  }

  public async Task HandleMessage(IMessage m) {
    var msg = m as IUserMessage;
    if (msg == null || msg.Author.IsBot || msg.Author.IsMe())
      return;
    var user = await Bot.Database.GetUser(msg.Author);
    if(user.IsBlacklisted)
      return;

    // Marks where the command begins
    var argPos = 0;

    // Determine if the msg is a command, based on if it starts with the defined command prefix 
    if (!msg.HasCharPrefix(Config.CommandPrefix, ref argPos))
      return;

    if (!msg.Channel.AllowCommands()) {
      Log.Info($"Attempted to run a command that is not allowed. {msg.Content.DoubleQuote()}");
      return;
    }

    // Execute the command. (result does not indicate a return value, 
    // rather an object stating if the command executed succesfully)
    var context = new CommandContext(Bot.Client, msg);
    var result = await Commands.Execute(context, argPos);
    var guildChannel = msg.Channel as ITextChannel;
    string channelMsg = guildChannel != null ? $"in {guildChannel.Name} on {guildChannel.Guild.ToIDString()}." 
      : "in private channel.";
    if (result.IsSuccess) {
      Log.Info($"Command successfully executed {msg.Content.DoubleQuote()} {channelMsg}");
      Bot.Counters.Get("command-success").Increment();
      return;
    }
    if (await CustomCommandCheck(msg, argPos))
      return;
    Log.Error($"Command failed {msg.Content.DoubleQuote()} {channelMsg} ({result.Error})");
    Bot.Counters.Get("command-failed").Increment();
    switch (result.Error) {
      // Ignore these kinds of errors, no need for response.
      case CommandError.UnknownCommand:
        return;
      default:
        Log.Info(result.ErrorReason);
        await msg.Respond(result.ErrorReason);
        break;
    }
  }

  async Task<bool> CustomCommandCheck(IMessage msg, int argPos) {
    var customCommandCheck = msg.Content.Substring(argPos).SplitWhitespace();
    if (customCommandCheck.Length <= 0)
      return false;
    var commandName = customCommandCheck[0];
    argPos += commandName.Length;
    var guild = (msg.Channel as ITextChannel)?.Guild;
    if(guild == null)
      return false;
    var command = Bot.Database.Commands.FirstOrDefault(c => c.GuildId == guild.Id && c.Name == commandName);
    if (command == null)
      return false;
    await command.Execute(msg, msg.Content.Substring(argPos));
    Bot.Counters.Get("custom-command-executed").Increment();
    return true;
  }


}

}
