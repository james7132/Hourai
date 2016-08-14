using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace DrumBot {

    [Module]
    public class Adnim {

        [Command("kick")]
        [Description( "Kicks all mentioned users. Requires ``Kick Members`` permission.")]
        public async Task Kick(IMessage message, 
            [Description("The users to kick")] params IGuildUser[] users) {
            var action = await CommandUtility.Action(message, "kick", async u => await u.KickAsync());
            await CommandUtility.ForEvery(message, users, action);
        }

        [Command("ban")]
        [Description( "Bans all mentioned users. Requires ``BanAsync Members`` permission.")]
        public async Task Ban(IMessage message, 
            [Description("The users to ban")] params IGuildUser[] users) {
            var action = await CommandUtility.Action(message, "ban", async u => await u.BanAsync());
            await CommandUtility.ForEvery(message, users, action);
        }

        [Command("mute")]
        [Description( "Server mutes all mentioned users. Requires ``MuteAsync Members`` permission.")]
        public async Task Mute(IMessage message, 
            [Description("The users to mute")] params IGuildUser[] users) {
            var action = await CommandUtility.Action(message, "mute", async u => await u.MuteAsync());
            await CommandUtility.ForEvery(message, users, action);
        }

        [Command("unmute")]
        [Description( "Server unmutes all mentioned users. Requires ``MuteAsync Members`` permission.")]
        public async Task Unmute(IMessage message, 
            [Description("The users to unmute")] params IGuildUser[] users) {
            var action = await CommandUtility.Action(message, "unmute", async u => await u.UnmuteAsync());
            await CommandUtility.ForEvery(message, users, action);
        }

        [Command("deafen")]
        [Description( "Server deafens all mentioned users. Requires ``DeafenAsync Members`` permission.")]
        public async Task Deafen(IMessage message, 
            [Description("The users to deafen")] params IGuildUser[] users) {
            var action = await CommandUtility.Action(message, "deafen", async u => await u.DeafenAsync());
            await CommandUtility.ForEvery(message, users, action);
        }

        [Command("undeafen")]
        [Description( "Server undeafens all mentioned users. Requires ``DeafenAsync Members`` permission.")]
        public async Task Nickname(IMessage message, 
            [Description("The users to undeafen")]params IGuildUser[] users) {
            var action = await CommandUtility.Action(message, "undeafen", async u => await u.UndeafenAsync());
            await CommandUtility.ForEvery(message, users, action);
        }

        [Command("nickname")]
        [Description("Sets the nickname of all mentioned users. Requires ``Manage Nicknames`` permission.")]
        public async Task Nickname(IMessage message, 
            [Description("The nickname to set")] string nickname,
            [Description("The users to undeafen")] params IGuildUser[] users) {
            Check.InGuild(message);
            IGuildUser[] allUsers = users;
            if (allUsers.Length <= 0)
                allUsers = new[] {message.Author as IGuildUser};
            var action = await CommandUtility.Action(message, "nickname", async u => await u.SetNickname(nickname));
            await CommandUtility.ForEvery(message, allUsers, action);
        }

        [Command("prune")]
        [Description("Removes the last X messages from the current channel. Requires ``Manage Messages`` permission.")]
        public async Task PruneUser(IMessage message, 
            [Description("The number of messages to prune")] int count) {
            await PruneMessages(Check.InGuild(message), m => true, count);
        }

        [Command("prune user")]
        [Description("Removes all messages from all mentioned users in the last 100 messages. Requires ``Manage Messages`` permission.")]
        public async Task PruneUser(IMessage message, 
            [Description("The users to prune the messages of")] params IGuildUser[] users) {
            var userSet = new HashSet<IUser>(users);
            await PruneMessages(Check.InGuild(message), u => userSet.Contains(u.Author));
        }

        [Command("channel create")]
        [Description("Creates a public channel with a specified name. Requires ``Manage Channels`` permission.")]
        public async Task ChannelCreate(IMessage message, string name) {
            var guild = Check.InGuild(message).Guild;
            var channel = await guild.CreateTextChannelAsync(name); 
            await message.Success($"{channel.Mention} created.");
        }

        [Command("channel delete")]
        [Description("Deletes all mentioned channels. Requires ``Manage Channels`` permission.")]
        public async Task ChannelDelete(IMessage message, params IGuildChannel[] channels) {
            await CommandUtility.ForEvery(message, channels, CommandUtility.Action(
                async delegate(IGuildChannel channel) {
                    await channel.DeleteAsync();
                }));
        }

        static async Task<string> PruneMessages(ITextChannel channel,
                                                Func<IMessage, bool> pred = null,
                                                int count = 100) {
            if (count > Config.PruneLimit)
                count = Config.PruneLimit;
            if (count < 0)
                return "Cannot prune a negative count of messages";
            var finalCount = count;
            var messages = await channel.GetMessagesAsync(count);
            IEnumerable<IMessage> allMessages = messages;
            if (pred != null) {
                var filtered = messages.Where(pred).ToArray();
                finalCount = Math.Min(finalCount, filtered.Length);
                allMessages = filtered;
            }
            await channel.DeleteMessagesAsync(allMessages
                                              .OrderByDescending(m => m.Timestamp)
                                              .Take(count));
            return Utility.Success($"Deleted {finalCount} messages.");
        }

        const string Requirements = " Requires ``Manage Role`` permission for both user and bot.";

        public async Task<string> Compose<T>(IEnumerable<T> targets, Func<T, Task<string>> conv) {
            string[] results = await Task.WhenAll(Check.NotNull(targets).Select(conv));
            return results.Where(s => !s.IsNullOrEmpty()).Join("\n");
        }

        [Command("role add")]
        [Description("Adds a role to all mentioned users." + Requirements)]
        public async Task Add(IMessage message, 
          [Description("The role to add.")] IRole role, 
          [Description("The users to add the role to.")]params IGuildUser[] users) {
            var action = await CommandUtility.Action(message, "add role", async u => await u.AddRolesAsync(role));
            await CommandUtility.ForEvery(message, users, action);
        }

        [Command("role remove")]
        [Description("Removes a role to all mentioned users." + Requirements)]
        public async Task Remove(IMessage message, 
          [Description("The role to remove.")] IRole role, 
          [Description("The users to remove the role to.")]params IGuildUser[] users) {
            var action = await CommandUtility.Action(message, "remove role", async u => await u.RemoveRolesAsync(role));
            await CommandUtility.ForEvery(message, users, action);
        }

        [Command("role nuke")]
        [Description("Removes a role to all users on the server." + Requirements)]
        public async Task Nuke(IMessage message, 
          [Description("The role to remove.")] IRole role) {
            var users = await Check.InGuild(message).Guild.GetUsersAsync();
            var action = await CommandUtility.Action(message, "remove role", async u => await u.RemoveRolesAsync(role));
            await CommandUtility.ForEvery(message, users, action);
        }

        [Command("role ban")]
        [Description("Bans all mentioned users from a specified role." + Requirements)]
        public async Task Ban(IMessage message,
          [Description("The role to add.")] IRole role, 
          [Description("The users to add the role to.")]params IGuildUser[] users) {
            var guildConfig = Config.GetGuildConfig(Check.InGuild(message).Guild);
            var action = await CommandUtility.Action(message, "ban",
                async u => {
                    await u.RemoveRolesAsync(role);
                    guildConfig.GetUserConfig(u).BanRole(role);
                });
            await CommandUtility.ForEvery(message, users, action);
        }

        [Command("role unban")]
        [Description("Unban all mentioned users from a specified role." + Requirements)]
        public async Task Unban(IMessage message,
          [Description("The role to unban.")] IRole role, 
          [Description("The users to unbanthe role to.")]params IGuildUser[] users) {
            var guildConfig = Config.GetGuildConfig(Check.InGuild(message).Guild);
            var action = await CommandUtility.Action(message, "ban",
                u => {
                    guildConfig.GetUserConfig(u).UnbanRole(role);
                    return Task.CompletedTask;
                });
            await CommandUtility.ForEvery(message, users, action);
        }

        [Command("role create")]
        [Description("Creates a mentionable role and applies it to all mentioned users")]
        public async Task Create(IMessage message, 
            [Description("Name of the role to create.")] string name) {
            var guild = Check.InGuild(message).Guild;
            await guild.CreateRoleAsync(name);
            await message.Success();
        }

        [Command("role delete")]
        public async Task RoleDelete(IMessage message, params IRole[] roles) {
            await CommandUtility.ForEvery(message, roles, CommandUtility.Action(
                async delegate(IRole role) {
                    await role.DeleteAsync(); 
                }));
        }

        static async Task RoleCommand(IMessage m, IRole role, string action, IEnumerable<IGuildUser> users, Func<IGuildUser, IRole, Task> task) {
            var guild = Check.InGuild(m).Guild;
            var selfUser = await Bot.Client.GetCurrentUserAsync();
            var guildBot = await guild.GetUserAsync(selfUser.Id);
            if (!Utility.RoleCheck(guildBot, role))
                throw new RoleRankException($"{guildBot.Username} cannot {action} role \"{role.Name}\", as it is above my roles.");
            if (!Utility.RoleCheck(m.Author as IGuildUser, role))
                throw new RoleRankException($"{m.Author.Username} cannot {action} role \"{role.Name}\", as it is above their roles.");
            await CommandUtility.ForEvery(m, users,
                await CommandUtility.Action(m, action + " role", user => task(user, role)));
        }
    }
}
