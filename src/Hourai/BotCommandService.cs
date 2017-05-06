using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Hourai.Model;
using Hourai.Custom;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Hourai {

[Service]
public class BotCommandService {

  readonly DiscordBotConfig _config;
  readonly IServiceProvider _services;
  readonly DiscordShardedClient _client;
  readonly CommandService _commands;
  readonly ErrorService _errors;
  readonly CustomConfigService _configService;
  readonly CounterSet _counters;

  readonly ILogger _log;

  public BotCommandService(IServiceProvider services,
                           IOptions<DiscordBotConfig> config,
                           DiscordShardedClient client,
                           CommandService commands,
                           CustomConfigService custom,
                           ErrorService errors,
                           CounterSet counters,
                           ILoggerFactory loggerFactory) {
    _services = Check.NotNull(services);
    _client = client;
    _commands = commands;
    _config = config.Value;
    _configService = custom;
    _counters = counters;
    _errors = errors;

    _log = loggerFactory.CreateLogger("Commands");

    _client.MessageReceived += HandleMessage;
    if (_commands != null) {
      _log.LogInformation($"Loaded modules: {_commands.Modules.Select(m => m.Name).Join(", ")}");
      _log.LogInformation($"Loaded commands: {_commands.Commands.Select(c => c.GetFullName()).Join(", ")}");
    }
  }

  public async Task HandleMessage(IMessage m) {
    var msg = m as SocketUserMessage;
    if (msg == null ||
        msg.Author.IsBot ||
        msg.Author?.Id == _client?.CurrentUser?.Id)
      return;
    using (var db = _services.GetService<BotDbContext>()) {
      // Marks where the command begins
      var argPos = 0;

      Guild dbGuild = null;
      char prefix = _config.CommandPrefix;
      var guild = (m.Channel as IGuildChannel)?.Guild;
      if(guild != null) {
        dbGuild = await db.Guilds.Get(guild);
        var prefixString = dbGuild.Prefix;
        if(string.IsNullOrEmpty(prefixString)) {
          prefix = _config.CommandPrefix;
          dbGuild.Prefix = prefix.ToString();
        } else {
          prefix = prefixString[0];
        }
      }

      // Determine if the msg is a command, based on if it starts with the defined command prefix
      if (!msg.HasCharPrefix(prefix, ref argPos))
        return;

      var user = await db.Users.Get(msg.Author);
      if(user.IsBlacklisted)
        return;

      // Execute the command. (result does not indicate a return value,
      // rather an object stating if the command executed succesfully)
      var context = new HouraiContext(_client, msg, db, user, dbGuild);
      await ExecuteCommand(context, argPos);
    }
  }

  public async Task ExecuteCommand(HouraiContext context, int argPos = 0) {
    var command = context.Message.Content.Substring(argPos);
    if (context.Guild != null) {
      var customConfig = await _configService.GetConfig(context.Guild);
      if (customConfig.Aliases != null) {
        foreach (var alias in customConfig.Aliases) {
          if (command.StartsWith(alias.Key)) {
            await ExecuteStandardCommand(context, context.Process(alias.Value));
            return;
          }
        }
      }
    }
    if (await ExecuteStandardCommand(context, command))
      return;
    if (await CustomCommandCheck(context, command))
      return;
  }

  async Task<bool> ExecuteStandardCommand(HouraiContext context, string command) {
    var result = await _commands.ExecuteAsync(context, command, _services);
    var guildChannel = context.Channel as ITextChannel;
    string channelMsg = guildChannel != null ? $"in {guildChannel.Name} on {guildChannel.Guild.ToIDString()}."
      : "in private channel.";
    if (result.IsSuccess) {
      _log.LogInformation($"Command successfully executed {context.Message.Content.DoubleQuote()} {channelMsg}");
      _counters.Get("command-success").Increment();
      return true;
    }
    switch (result.Error) {
      // Ignore these kinds of errors, no need for response.
      case CommandError.UnknownCommand:
        break;
      default:
        _log.LogError($"Command failed {command.DoubleQuote()} {channelMsg} ({result.Error})");
        _counters.Get("command-failed").Increment();
        if(result is ExecuteResult) {
          var executeResult = (ExecuteResult) result;
          _errors.RegisterException(executeResult.Exception);
          _log.LogError(0, executeResult.Exception, "Command Failed.");
        } else {
          _log.LogError(result.ErrorReason);
        }
        await context.Channel.Respond(result.ErrorReason);
        break;
    }
    return false;
  }

  async Task<bool> CustomCommandCheck(HouraiContext msg, string cmd) {
    var customCommandCheck = cmd.SplitWhitespace();
    if (customCommandCheck.Length <= 0 || msg.Guild == null)
      return false;
    var commandName = customCommandCheck[0];
    cmd = cmd.Substring(commandName.Length);
    msg.Input = cmd.Trim();
    var command = await msg.Db.Commands.FindAsync(msg.Guild.Id, commandName);
    if (command == null)
      return false;
    await command.Execute(msg, cmd);
    _counters.Get("custom-command-executed").Increment();
    return true;
  }

}

}
