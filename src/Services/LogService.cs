using System;
using System.Linq;
using Discord;
using Discord.Commands;

namespace DrumBot {
    public class LogService : IService {

        public ChannelSet ChannelSet { get; }
        
        public LogService(ChannelSet set) { ChannelSet = set; }

        public void Install(DiscordClient client) {
            client.ServerAvailable += ServerLog(client, "Discovered");
            client.ServerUnavailable += ServerLog(client, "Lost");

            client.ChannelCreated += ChannelLog("created");
            client.ChannelDestroyed += ChannelLog("removed");
            
            client.UserBanned += UserLog("was banned from");
            client.UserUnbanned += UserLog("was unbanned from");
            client.UserJoined += UserLog("joined");
            client.UserLeft += UserLog("left");

            client.MessageUpdated += delegate(object sender, MessageUpdatedEventArgs e) {
                var log = $"Message update by {e.User.ToIDString()} ";
                if (e.Channel.IsPrivate)
                    log += "in private channel.";
                else
                    log += $"in {e.Channel.Name} on {e.Server.ToIDString()}";
                Log.Info(log);
            };
            client.MessageDeleted += MessageLog("deleted");

            client.RoleCreated += RoleLog("created");
            client.RoleUpdated += delegate(object sender, RoleUpdatedEventArgs e) {
                Log.Info($"Role { e.After.Name } on { e.Server.ToIDString() } was updated.");
            };
            client.RoleDeleted += RoleLog("deleted");

            var commandService = client.GetService<CommandService>();
            if(commandService != null)
                commandService.CommandExecuted +=
                    delegate(object s, CommandEventArgs e) {
                        var log = $"Command {e.Command.Text} executed by {e.User.ToIDString()} ";
                        if (e.Channel.IsPrivate)
                            log += "in private channel.";
                        else
                            log += $"in {e.Channel.Name} on {e.Server.ToIDString()}";
                        Log.Info(log);
                    };

            // Log every public message not made by the bot.
            client.MessageReceived +=
                async (s, e) => {
                    if (e.Message.IsAuthor || e.Channel.IsPrivate)
                        return;
                    await ChannelSet.Get(e.Channel).LogMessage(e.Message);
                };

            // Make sure that every channel is available on loading up a server.
            client.ServerAvailable += delegate(object sender, ServerEventArgs e) {
                foreach (Channel channel in e.Server.TextChannels)
                    ChannelSet.Get(channel);
            };
            
            // Keep up to date with channels
            client.ChannelCreated += (s, e) => ChannelSet.Get(e.Channel);

            // Preserve logs from deleted channels
            client.ChannelDestroyed += async delegate(object sender, ChannelEventArgs evt) {
                await ChannelSet.Get(evt.Channel).DeletedChannel(evt.Channel);
            };
        }

        EventHandler<RoleEventArgs> RoleLog(string eventType) {
            return delegate(object sender, RoleEventArgs e) {
                Log.Info($"Role { e.Role.Name } on { e.Server.ToIDString() } was { eventType }.");
            };
        }

        EventHandler<MessageEventArgs> MessageLog(string eventType) {
            return delegate(object sender, MessageEventArgs e) {
                Log.Info($"Message on { e.Channel.Name } on { e.Server.ToIDString() } was { eventType }.");
            };
        }

        EventHandler<UserEventArgs> UserLog(string eventType) {
            return delegate (object sender, UserEventArgs e) {
                Log.Info($"User { e.User.ToIDString() } {eventType} { e.Server.ToIDString() }");
            };
        }

        EventHandler<ChannelEventArgs> ChannelLog(string eventType) {
            return delegate (object sender, ChannelEventArgs e) {
                Log.Info($"Channel {eventType}: {e.Channel.ToIDString()} on server {e.Server.ToIDString()}");
            };
        }

        EventHandler<ServerEventArgs> ServerLog(DiscordClient client, string eventType) {
            return delegate (object sender, ServerEventArgs e) {
                    Log.Info($"{eventType} server {e.Server.ToIDString()}. Server Count: { client.Servers.Count() }");
            };
        }
    }
}
