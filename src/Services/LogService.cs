using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace DrumBot {
    public class LogService {

        public ChannelSet ChannelSet { get; }
        
        public LogService(ChannelSet set) { ChannelSet = set; }

        public LogService(DiscordSocketClient client, CommandService service = null) {
            client.GuildAvailable += ServerLog("Discovered");
            client.GuildUnavailable += ServerLog("Lost");

            client.ChannelCreated += ChannelLog("created");
            client.ChannelDestroyed += ChannelLog("removed");

            client.UserBanned += (u, g) => UserLog("was banned from")(u);
            client.UserUnbanned += (u, g) => UserLog("was unbanned from")(u);
            client.UserJoined += UserLog("joined");
            client.UserLeft += UserLog("left");

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
            client.MessageUpdated += async delegate(Optional<IMessage> before, IMessage after) {
                var log = $"Message update by { after.Author.ToIDString()} ";
                var guildChannel = after.Channel as IGuildChannel;
                if (guildChannel == null)
                    log += "in private channel.";
                else
                    log += $"in { guildChannel.Name} on {guildChannel.Guild.ToIDString()}";
                Log.Info(log);
            };
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
            client.MessageDeleted += (i,u) => MessageLog("deleted")(u.Value);

            client.RoleCreated += RoleLog("created");
            client.RoleUpdated += async delegate(IRole before, IRole after) {
                var guild = await Bot.Client.GetGuildAsync(after.GuildId);
                Log.Info($"Role { after.Name } on { guild.ToIDString() } was updated.");
            };
            client.RoleDeleted += RoleLog("deleted");

            //TODO: Reimplement
            //service.CommandService +=
            //    delegate(object s, CommandEventArgs e) {
            //        var log = $"CommandUtility {e.CommandUtility.Text} executed by {e.User.ToIDString()} ";
            //        if (e.Channel.IsPrivate)
            //            log += "in private channel.";
            //        else
            //            log += $"in {e.Channel.Name} on {e.Server.ToIDString()}";
            //        Log.Info(log);
            //    };

            // Log every public message not made by the bot.
            client.MessageReceived +=
                async m => {
                    var channel = m.Channel as ITextChannel;
                    if (m.IsAwthor() || channel == null)
                        return;
                    await ChannelSet.Get(channel).LogMessage(m);
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
                var guild = await Bot.Client.GetGuildAsync(role.GuildId);
                Log.Info($"Role { role.Name } on { guild.ToIDString() } was { eventType }.");
            };
        }

        Func<IMessage, Task> MessageLog(string eventType) {
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
            return async delegate(IMessage message) {
                var guildChannel = message.Channel as IGuildChannel;
                var privateChannel = message.Channel as IPrivateChannel;
                if (guildChannel != null) {
                    Log.Info($"Message on {guildChannel.Name} on {guildChannel.Guild.ToIDString()} was {eventType}.");
                } else if(privateChannel != null) {
                    Log.Info($"Private message to {privateChannel.Recipients} was {eventType}.");
                } else {
                    Log.Error($"Action {eventType.DoubleQuote()} occured to a message instance of type {message.GetType()} and was unhandled");
                }
            };
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        }

        Func<IUser, Task> UserLog(string eventType) {
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
            return async delegate (IUser user) {
                var guildUser = user as IGuildUser;
                var selfUser = user as ISelfUser;
                if (guildUser != null) {
                    Log.Info($"User {guildUser.ToIDString()} {eventType} {guildUser.Guild.ToIDString()}");
                } else if(selfUser != null) {
                    Log.Info($"User {selfUser.ToIDString()} {eventType}");
                } else {
                    Log.Error($"Action {eventType.DoubleQuote()} occured to a user instance of type {user.GetType()} and was unhandled");
                }
            };
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        }

        Func<IChannel, Task> ChannelLog(string eventType) {
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
            return async delegate (IChannel channel) {
                var guildChannel = channel as IGuildChannel;
                if(guildChannel != null)
                    Log.Info($"Channel {eventType}: {guildChannel.ToIDString()} on server {guildChannel.Guild.ToIDString()}");

            };
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        }

        Func<IGuild, Task> ServerLog(string eventType) {
            return delegate (IGuild g) {
                Log.Info($"{eventType} guild {g.ToIDString()}.");
                return Task.CompletedTask;
            };
        }
    }
}
