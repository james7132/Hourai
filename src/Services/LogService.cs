using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Discord;
using Discord.WebSocket;

namespace Hourai {

public class LogService {

  public LogSet Logs { get; }

  public LogService(DiscordSocketClient client, LogSet set) {
    Logs = set;
    client.Log += delegate(LogMessage message) {
      switch (message.Severity) {
        case LogSeverity.Critical:
        case LogSeverity.Error:
          Log.Error(message.Message);
          break;
        case LogSeverity.Warning:
          Log.Warning(message.Message);
          break;
        case LogSeverity.Info:
          Log.Info(message.Message);
          break;
        case LogSeverity.Verbose:
        case LogSeverity.Debug:
          Log.Debug(message.Message);
          break;
        default:
          throw new ArgumentOutOfRangeException();
      }
      if(message.Exception != null)
        Log.Error(message.Exception);
      return Task.CompletedTask;
    };

    client.GuildAvailable += GuildLog("Discovered");
    client.GuildUnavailable += GuildLog("Lost");

    client.ChannelCreated += ChannelLog("created");
    client.ChannelDestroyed += ChannelLog("removed");

    client.RoleCreated += RoleLog("created");
    client.RoleDeleted += RoleLog("deleted");

    client.UserJoined += UserLog("joined");
    client.UserLeft += UserLog("left");
    client.UserBanned += (u, g) => UserLog("banned")(u);
    client.UserUnbanned += (u, g) => UserLog("unbanned")(u);

    // Log every public message not made by the bot.
    client.MessageReceived += m => {
      var channel = m.Channel as ITextChannel;
      if (m.Author.IsMe() || m.Author.IsBot ||channel == null)
          return Task.CompletedTask;
      return Logs.GetChannel(channel).LogMessage(m);
    };

    //// Make sure that every channel is available on loading up a server.
    client.GuildAvailable += DownloadGuildChatLogs;
    client.JoinedGuild += DownloadGuildChatLogs;

    // Keep up to date with channels
    client.ChannelCreated += channel => {
      var textChannel = channel as ITextChannel;
      if (textChannel != null)
        Logs.GetChannel(textChannel);
      return Task.CompletedTask;
    };

    // Preserve logs from deleted channels
    client.ChannelDestroyed += async channel => {
      var textChannel = channel as ITextChannel;
      if (textChannel != null)
        await Logs.GetChannel(textChannel).DeletedChannel(textChannel);
    };

    client.GuildUpdated += GuildUpdated;
    client.UserUpdated += UserUpdated;
    client.RoleUpdated += RoleUpdated; 
    client.ChannelUpdated += ChannelUpdated;
  }

  Task LogChange<T, TA>(GuildLog log,
                       string change,
                       T b, 
                       T a, 
                       Func<T, TA> check) {
    var valA = check(a);
    var valB = check(b);
    if(!EqualityComparer<TA>.Default.Equals(valA, valB))
      return log.LogEvent($"{change} changed: \"{valA}\" => \"{valB}\"");
    return Task.CompletedTask;
  }

  Task LogSetChange<T, TA>(GuildLog log,
                          string change,
                          T a,
                          T b,
                          Func<T, IEnumerable<TA>> check,
                          Func<TA, string> toString) {
    var ia = check(a);
    var ib = check(b);
    var bIa = ib.Except(ia);
    var aIb = ia.Except(ib);
    if(bIa.Any() || aIb.Any()) {
      var roleLog = $"{change} changed:";
      if(aIb.Any())
        roleLog += $" -[{aIb.Select(toString).Join(", ")}]";
      if(bIa.Any())
        roleLog += $" +[{bIa.Select(toString).Join(", ")}]";
      return log.LogEvent(roleLog);
    }
    return Task.CompletedTask;
  }

  async Task GuildUpdated(IGuild b, IGuild a) {
    var log = Logs.GetGuild(a);
    if(log == null)
      return;
    await LogChange(log, "Guild AFK Timeout", b, a, g => g.AFKTimeout);
    await LogChange(log, "Guild Icon", b, a, g => g.IconUrl);
    await LogChange(log, "Guild Default Message Notification", b, a, g => g.DefaultMessageNotifications);
    await LogChange(log, "Guild Embeddable State", b, a, g => g.IsEmbeddable);
    await LogChange(log, "Guild MFA Level", b, a, g => g.MfaLevel);
    await LogChange(log, "Guild Name", b, a, g => g.Name);
    await LogChange(log, "Guild Splash URL", b, a, g => g.SplashUrl);
    await LogChange(log, "Guild Verification Level", b, a, g => g.VerificationLevel);
    await LogChange(log, "Guild Voice Region ID", b, a, g => g.VoiceRegionId);
    await LogSetChange(log, "Guild Features", b, a, g => g.Features, f => f);
    await LogSetChange(log, "Guild Emojis", b, a, g => g.Emojis, e => e.Name);
    if(b.AFKChannelId != a.AFKChannelId)  {
      IGuildChannel bAfk = null, aAfk = null;
      if(b.AFKChannelId.HasValue)
        bAfk = await a.GetChannelAsync(b.AFKChannelId.Value);
      if(a.AFKChannelId.HasValue)
        aAfk = await a.GetChannelAsync(a.AFKChannelId.Value);
      await log.LogEvent($"Guild AFK Channel changed: {bAfk.ToIDString()} => {aAfk.ToIDString()}");
    }
  }

  async Task UserUpdated(IUser before, IUser after) {
    var b = before as IGuildUser;
    var a = after as IGuildUser;
    if(b == null ||  a == null)
      return;
    var log = Logs.GetGuild(a.Guild);
    var userString = a.ToIDString();
    await LogChange(log, $"User {userString} Username", b, a, u => u.Username);
    await LogChange(log, $"User {userString} Nickname", b, a, u => u.Nickname);
    await LogSetChange(log, $"User {userString} Roles", b, a,
        u => u.Roles, r => r.ToIDString());
  }

  async Task RoleUpdated(IRole b, IRole a) {
    var guild = await Bot.Client.GetGuildAsync(a.GuildId);
    if(guild == null)
      return;
    var log = Logs.GetGuild(guild);
    var roleString = a.ToIDString();
    await LogChange(log, $"Role {roleString} Color", b, a, r => r.Color);
    await LogChange(log, $"Role {roleString} User List Seperation", b, a, r => r.IsHoisted);
    await LogChange(log, $"Role {roleString} Name", b, a, r => r.Name);
    await LogChange(log, $"Role {roleString} Position", b, a, r => r.Position);
    await LogSetChange(log, $"Role {roleString} Permissions", b, a, r => r.Permissions.ToList(), p => p.ToString());
  }

  async Task ChannelUpdated(IChannel before, IChannel after) {
    var b = before as IGuildChannel;
    var a = after as IGuildChannel;
    if(b == null || a == null)
      return;
    var log = Logs.GetGuild(a.Guild);
    await LogChange(log, $"Channel {a.ToIDString()} Name", b, a, c => c.Name);
    await LogChange(log, $"Channel {a.ToIDString()} Position", b, a, c => c.Position);
    //TODO(james7132): Add Permission Overwrites
  }

  async Task DownloadGuildChatLogs(IGuild guild) {
    foreach (ITextChannel channel in guild.GetTextChannels())
      await Logs.AddChannel(channel);
  }

  Func<IRole, Task> RoleLog(string eventType) {
    return async delegate(IRole role) {
      if(role == null) {
        Log.Info($"Role {eventType}.");
        return;
      }
      var guild = await Bot.Client.GetGuildAsync(role.GuildId);
      await Logs.GetGuild(guild).LogEvent($"Role {eventType}: { role.Name }");
    };
  }

  Func<IMessage, Task> MessageLog(string eventType) {
    return delegate(IMessage message) {
      if(message == null) {
        Log.Info($"Message {eventType}.");
        return Task.CompletedTask;
      }
      var guildChannel = message.Channel as IGuildChannel;
      var privateChannel = message.Channel as IPrivateChannel;
      if (guildChannel != null) {
        Log.Info($"Message on {guildChannel.Name} on {guildChannel.Guild.ToIDString()} was {eventType}.");
      } else if(privateChannel != null) {
        Log.Info($"Message to {privateChannel.Recipients} in private channel was {eventType}.");
      } else {
        Log.Error($"Action {eventType.DoubleQuote()} occured to a message instance of type {message.GetType()} and was unhandled");
      }
      return Task.CompletedTask;
    };
  }

  Func<IUser, Task> UserLog(string eventType) {
    return delegate (IUser user) {
      var guildUser = user as IGuildUser;
      var selfUser = user as ISelfUser;
      if (guildUser != null) {
        Logs.GetGuild(guildUser.Guild).LogEvent($"User {eventType}: {guildUser.ToIDString()}");
      } else if(selfUser != null) {
        Log.Info($"User {selfUser.ToIDString()} {eventType}");
      } else {
        Log.Error($"Action {eventType.DoubleQuote()} occured to a user instance of type {user.GetType()} and was unhandled");
      }
      return Task.CompletedTask;
    };
  }

  Func<IChannel, Task> ChannelLog(string eventType) {
    return delegate (IChannel channel) {
      var guildChannel = channel as IGuildChannel;
      if(guildChannel != null)
        Logs.GetGuild(guildChannel.Guild).LogEvent($"Channel {eventType}: {guildChannel.ToIDString()}");
      return Task.CompletedTask;
    };
  }

  Func<IGuild, Task> GuildLog(string eventType) {
    return delegate (IGuild g) {
      Log.Info($"{eventType} {g.ToIDString()}.");
      return Task.CompletedTask;
    };
  }

}

}
