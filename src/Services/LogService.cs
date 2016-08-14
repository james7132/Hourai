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
            // TODO: Reimplement
            //client.ServerAvailable += ServerLog(client, "Discovered");
            //client.ServerUnavailable += ServerLog(client, "Lost");

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
            // TODO: Reimplement
            //client.MessageReceived +=
            //    async (s, e) => {
            //        if (e.Message.IsAuthor || e.Channel.IsPrivate)
            //            return;
            //        await ChannelSet.Get(e.Channel).LogMessage(e.Message);
            //    };

            //// Make sure that every channel is available on loading up a server.
            // TODO: Reimplement
            //client.ServerAvailable += delegate(object sender, ServerEventArgs e) {
            //    foreach (Channel channel in e.Server.TextChannels)
            //        ChannelSet.Get(channel);
            //};j
            
            // Keep up to date with channels
            // TODO: Reimplement
            //client.ChannelCreated += (s, e) => ChannelSet.Get(e.Channel);

            // Preserve logs from deleted channels
            // TODO: Reimplement
            //client.ChannelDestroyed += async delegate(object sender, ChannelEventArgs evt) {
            //    await ChannelSet.Get(evt.Channel).DeletedChannel(evt.Channel);
            //};
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

        //TODO: Reevaluate necessity of this funciton 
        //EventHandler<ServerEventArgs> ServerLog(DiscordClient client, string eventType) {
        //    return delegate (object sender, ServerEventArgs e) {
        //            Log.Info($"{eventType} server {e.Server.ToIDString()}. Server CountValues: { client.Servers.CountValues() }");
        //    };
        //}
    }
}
