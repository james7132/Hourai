using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text;

namespace Hourai {

public class LogService {

  public LogSet Logs { get; }
  public DiscordSocketClient Client { get; } 
  public string BaseDirectory { get; } 
  public string BotLog { get; private set; }
  const string LogStringFormat = "yyyy-MM-dd_HH_mm_ss";

  public LogService(IDependencyMap map, string directory) {
    BaseDirectory = directory;
    Client = map.Get<DiscordSocketClient>();
    Logs = map.Get<LogSet>();
    SetupBotLog();
    ClientLogs();
    GuildLogs();
    ChannelLogs();
    RoleLogs();
    UserLogs();
    MessageLogs();
  }

  void SetupBotLog() {
    Console.OutputEncoding = Encoding.UTF8;
    var logDirectory = Path.Combine(BaseDirectory, Config.LogDirectory);
    if(!Directory.Exists(logDirectory))
      Directory.CreateDirectory(logDirectory);
    BotLog = Path.Combine(logDirectory, DateTime.Now.ToString(LogStringFormat) + ".log");
    Trace.Listeners.Clear();
    var botLogFile = new FileStream(BotLog, FileMode.Create, FileAccess.Write, FileShare.Read);
    Trace.Listeners.Add(new TextWriterTraceListener(botLogFile) {TraceOutputOptions = TraceOptions.ThreadId | TraceOptions.DateTime});
    Trace.Listeners.Add(new TextWriterTraceListener(Console.Out) {TraceOutputOptions = TraceOptions.DateTime});
    Trace.AutoFlush = true;
  }

  void ClientLogs() {
    Client.Log += delegate(LogMessage message) {
      switch (message.Severity) {
        case LogSeverity.Critical:
        case LogSeverity.Error:
          Log.Error(message.ToString());
          break;
        case LogSeverity.Warning:
          Log.Warning(message.ToString());
          break;
        case LogSeverity.Info:
          Log.Info(message.ToString());
          break;
        case LogSeverity.Verbose:
        case LogSeverity.Debug:
          Log.Debug(message.ToString());
          break;
        default:
          throw new ArgumentOutOfRangeException();
      }
      if(message.Exception != null)
        Log.Error(message.Exception);
      return Task.CompletedTask;
    };
  }


  void GuildLogs() {
    Client.GuildAvailable += GuildLog("Discovered");
    Client.GuildUnavailable += GuildLog("Lost");
    Client.GuildUpdated += GuildUpdated;
  }

  void ChannelLogs() {
    Client.ChannelCreated += ChannelLog("created");
    Client.ChannelDestroyed += ChannelLog("removed");
    Client.ChannelUpdated += ChannelUpdated;

  }

  void RoleLogs() {
    Client.RoleCreated += RoleLog("created");
    Client.RoleDeleted += RoleLog("deleted");
    Client.RoleUpdated += RoleUpdated; 
  }

  void UserLogs() {
    Client.UserJoined += UserLog("joined");
    Client.UserLeft += UserLog("left");
    Client.UserBanned += (u, g) => UserLog("banned")(u);
    Client.UserUnbanned += (u, g) => UserLog("unbanned")(u);
    Client.UserUpdated += UserUpdated;
  }

  void MessageLogs() {
    // Log every public message not made by the bot.
    Client.MessageReceived += m => {
      var channel = m.Channel as ITextChannel;
      if (m.Author.IsMe() || m.Author.IsBot ||channel == null)
        return Task.CompletedTask;
      return Logs.GetChannel(channel).LogMessage(m);
    };

    //// Make sure that every channel is available on loading up a server.
    Client.GuildAvailable += DownloadGuildChatLogs;
    Client.JoinedGuild += DownloadGuildChatLogs;

    // Keep up to date with channels
    Client.ChannelCreated += channel => {
      var textChannel = channel as ITextChannel;
      if (textChannel != null)
        Logs.GetChannel(textChannel);
      return Task.CompletedTask;
    };

    // Preserve logs from deleted channels
    Client.ChannelDestroyed += async channel => {
      var textChannel = channel as ITextChannel;
      if (textChannel != null)
        await Logs.GetChannel(textChannel).DeletedChannel(textChannel);
    };
  }

  Task LogChange<T, TA>(GuildLog log,
                       string change,
                       T b, 
                       T a, 
                       Func<T, TA> check) {
    var valA = check(a);
    var valB = check(b);
    if(!EqualityComparer<TA>.Default.Equals(valA, valB))
      return log.LogEvent($"{change} changed: \"{valB}\" => \"{valA}\"");
    return Task.CompletedTask;
  }

  Task LogSetChange<T, TA>(GuildLog log,
                          string change,
                          T b,
                          T a,
                          Func<T, IEnumerable<TA>> check,
                          Func<TA, string> toString) {
    var ia = check(a);
    var ib = check(b);
    var bIa = ib.Except(ia);
    var aIb = ia.Except(ib);
    if(bIa.Any() || aIb.Any()) {
      var roleLog = $"{change} changed:";
      if(aIb.Any())
        roleLog += $" +[{aIb.Select(toString).Join(", ")}]";
      if(bIa.Any())
        roleLog += $" -[{bIa.Select(toString).Join(", ")}]";
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
        u => u.GetRoles(), r => r.ToIDString());
  }

  async Task RoleUpdated(IRole b, IRole a) {
    var guild = a.Guild;
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
    var textChannels = await guild.GetTextChannelsAsync();
    foreach (ITextChannel channel in textChannels)
      await Logs.AddChannel(channel);
  }

  Func<IRole, Task> RoleLog(string eventType) {
    return async delegate(IRole role) {
      if(role == null) {
        Log.Info($"Role {eventType}.");
        return;
      }
      await Logs.GetGuild(role.Guild).LogEvent($"Role {eventType}: { role.Name }");
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
