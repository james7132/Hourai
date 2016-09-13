using System;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace DrumBot {

public class LogService {

  public ChannelSet ChannelSet { get; }

  public LogService(DiscordSocketClient client, ChannelSet set) {
    ChannelSet = set;
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
    client.GuildUpdated += (b, a) => GuildLog("updated")(a);
    client.GuildUnavailable += GuildLog("Lost");

    client.ChannelCreated += ChannelLog("created");
    client.ChannelUpdated += (b, a) => ChannelLog("updated")(a);
    client.ChannelDestroyed += ChannelLog("removed");

    client.UserBanned += (u, g) => UserLog("was banned from")(u);
    client.UserUpdated += (b, a) => UserLog("was updated")(a);
    client.UserUnbanned += (u, g) => UserLog("was unbanned from")(u);

    client.UserJoined += UserLog("joined");
    client.UserLeft += UserLog("left");

    client.MessageUpdated += (b, a) => MessageLog("updated")(a);
    client.MessageDeleted += (i, u) => MessageLog("deleted")(u.IsSpecified ? u.Value : null);

    client.RoleCreated += RoleLog("created");
    client.RoleUpdated += (b, a) => RoleLog("updated")(a); 
    client.RoleDeleted += RoleLog("deleted");

    // Log every public message not made by the bot.
    client.MessageReceived += m => {
      var channel = m.Channel as ITextChannel;
      if (m.Author.IsMe() || channel == null)
          return Task.CompletedTask;
      return ChannelSet.Get(channel).LogMessage(m);
    };

    //// Make sure that every channel is available on loading up a server.
    client.GuildAvailable += delegate (IGuild guild) {
      foreach (ITextChannel channel in guild.GetTextChannels())
        ChannelSet.Get(channel);
      return Task.CompletedTask;
    };

    // Keep up to date with channels
    client.ChannelCreated += channel => {
      var textChannel = channel as ITextChannel;
      if (textChannel != null)
        ChannelSet.Get(textChannel);
      return Task.CompletedTask;
    };

    // Preserve logs from deleted channels
    client.ChannelDestroyed += async channel => {
      var textChannel = channel as ITextChannel;
      if (textChannel != null)
        await ChannelSet.Get(textChannel).DeletedChannel(textChannel);
    };
  }

  Func<IRole, Task> RoleLog(string eventType) {
    return async delegate(IRole role) {
      if(role == null) {
        Log.Info($"Role {eventType}.");
        return;
      }
      var guild = await Bot.Client.GetGuildAsync(role.GuildId);
      Log.Info($"Role { role.Name } on { guild.ToIDString() } was { eventType }.");
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
        Log.Info($"User {guildUser.ToIDString()} {eventType} {guildUser.Guild.ToIDString()}");
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
        Log.Info($"Channel {eventType}: {guildChannel.ToIDString()} on server {guildChannel.Guild.ToIDString()}");
      return Task.CompletedTask;
    };
  }

  Func<IGuild, Task> GuildLog(string eventType) {
    return delegate (IGuild g) {
      Log.Info($"{eventType} guild {g.ToIDString()}.");
      return Task.CompletedTask;
    };
  }

}

}
