using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace DrumBot {

    [Module]
    [PublicOnly]
    public class Adnim {

        [Command("kick")]
        [Permission(GuildPermission.KickMembers)]
        [Description( "Kicks all mentioned users. Requires ``Kick Members`` permission.")]
        public async Task Kick(IMessage message, 
             params IGuildUser[] users) {
            var action = await CommandUtility.Action(message, "kick", async u => await u.KickAsync());
            await CommandUtility.ForEvery(message, users, action);
        }

        [Command("ban")]
        [Permission(GuildPermission.BanMembers)]
        [Description( "Bans all mentioned users. Requires ``Ban Members`` permission.")]
        public async Task Ban(IMessage message, 
             params IGuildUser[] users) {
            var action = await CommandUtility.Action(message, "ban", async u => await u.BanAsync());
            await CommandUtility.ForEvery(message, users, action);
        }

        [Command("mute")]
        [Permission(GuildPermission.MuteMembers)]
        [Description( "Server mutes all mentioned users. Requires ``Mute Members`` permission.")]
        public async Task Mute(IMessage message, params IGuildUser[] users) {
            var action = await CommandUtility.Action(message, "mute", async u => await u.MuteAsync());
            await CommandUtility.ForEvery(message, users, action);
        }

        [Command("unmute")]
        [Permission(GuildPermission.MuteMembers)]
        [Description( "Server unmutes all mentioned users. Requires ``Mute Members`` permission.")]
        public async Task Unmute(IMessage message, params IGuildUser[] users) {
            var action = await CommandUtility.Action(message, "unmute", async u => await u.UnmuteAsync());
            await CommandUtility.ForEvery(message, users, action);
        }

        [Command("deafen")]
        [Permission(GuildPermission.DeafenMembers)]
        [Description( "Server deafens all mentioned users. Requires ``Deafen Members`` permission.")]
        public async Task Deafen(IMessage message, params IGuildUser[] users) {
            var action = await CommandUtility.Action(message, "deafen", async u => await u.DeafenAsync());
            await CommandUtility.ForEvery(message, users, action);
        }

        [Command("undeafen")]
        [Permission(GuildPermission.DeafenMembers)]
        [Description( "Server undeafens all mentioned users. Requires ``Deafen Members`` permission.")]
        public async Task Undeafen(IMessage message, params IGuildUser[] users) {
            var action = await CommandUtility.Action(message, "undeafen", async u => await u.UndeafenAsync());
            await CommandUtility.ForEvery(message, users, action);
        }

        [Command("nickname")]
        [Description("Sets the nickname of all mentioned users, or nicknames yourself.\nIf no ``users`` is empty, nicknames the user who used the command"
            + "and requires the ``Change Nickname`` permission.\nIf at least one ``user`` is specified, nicknames the mentioned users and requires the "
            + "``Manage Nicknames`` permission.")]
        public async Task Undeafen(IMessage message, string nickname, params IGuildUser[] users) {
            Check.InGuild(message);
            var author = message.Author as IGuildUser;
            IGuildUser[] allUsers = users;
            if (allUsers.Length <= 0) {
                if(!author.GuildPermissions.ChangeNickname) {
                    await message.Respond($"{author.Mention} you do not have the ``Change Nickname`` permission. See ``{Config.CommandPrefix}help nickname``");
                    return;
                }
                allUsers = new[] {message.Author as IGuildUser};
            }
            if(!author.GuildPermissions.ManageNicknames) {
                await message.Respond($"{author.Mention} you do not have the ``Manage Nicknames`` permission. See ``{Config.CommandPrefix}help nickname``");
                return;
            }

            var action = await CommandUtility.Action(message, "nickname", async u => await u.SetNickname(nickname));
            await CommandUtility.ForEvery(message, allUsers, action);
        }

        [Command("prune")]
        [Permission(GuildPermission.ManageMessages)]
        [Description("Removes the last X messages from the current channel. Requires ``Manage Messages`` permission.")]
        public async Task Prune(IMessage message, int count) {
            await PruneMessages(Check.InGuild(message), m => true, count);
        }

        [Command("prune user")]
        [Permission(GuildPermission.ManageMessages)]
        [Description("Removes all messages from all mentioned users in the last 100 messages. Requires ``Manage Messages`` permission.")]
        public async Task PruneUser(IMessage message, params IGuildUser[] users) {
            var userSet = new HashSet<IUser>(users);
            await PruneMessages(Check.InGuild(message), u => userSet.Contains(u.Author));
        }

        [Command("channel create")]
        [Permission(GuildPermission.ManageChannels)]
        [Description("Creates a public channel with a specified name. Requires ``Manage Channels`` permission.")]
        public async Task ChannelCreate(IMessage message, string name) {
            var guild = Check.InGuild(message).Guild;
            var channel = await guild.CreateTextChannelAsync(name); 
            await message.Success($"{channel.Mention} created.");
        }

        [Command("channel delete")]
        [Permission(GuildPermission.ManageChannels)]
        [Description("Deletes all mentioned channels. Requires ``Manage Channels`` permission.")]
        public Task ChannelDelete(IMessage message, params IGuildChannel[] channels) {
            return CommandUtility.ForEvery(message, channels, CommandUtility.Action(
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
        [Permission(GuildPermission.ManageRoles)]
        [Description("Adds a role to all mentioned users." + Requirements)]
        public async Task Add(IMessage message, IRole role, params IGuildUser[] users) {
            var action = await CommandUtility.Action(message, "add role", async u => await u.AddRolesAsync(role));
            await CommandUtility.ForEvery(message, users, action);
        }

        [Command("role remove")]
        [Permission(GuildPermission.ManageRoles)]
        [Description("Removes a role to all mentioned users." + Requirements)]
        public async Task Remove(IMessage message, IRole role, params IGuildUser[] users) {
            var action = await CommandUtility.Action(message, "remove role", async u => await u.RemoveRolesAsync(role));
            await CommandUtility.ForEvery(message, users, action);
        }

        [Command("role nuke")]
        [Permission(GuildPermission.ManageRoles)]
        [Description("Removes a role to all users on the server." + Requirements)]
        public async Task Nuke(IMessage message, IRole role) {
            var users = await Check.InGuild(message).Guild.GetUsersAsync();
            var action = await CommandUtility.Action(message, "remove role", async u => await u.RemoveRolesAsync(role));
            await CommandUtility.ForEvery(message, users, action);
        }

        [Command("role ban")]
        [Permission(GuildPermission.ManageRoles)]
        [Description("Bans all mentioned users from a specified role." + Requirements)]
        public async Task RoleBan(IMessage message, IRole role, params IGuildUser[] users) {
            var guildConfig = Config.GetGuildConfig(Check.InGuild(message).Guild);
            var action = await CommandUtility.Action(message, "ban",
                async u => {
                    await u.RemoveRolesAsync(role);
                    guildConfig.GetUserConfig(u).BanRole(role);
                });
            await CommandUtility.ForEvery(message, users, action);
        }

        [Command("role unban")]
        [Permission(GuildPermission.ManageRoles)]
        [Description("Unban all mentioned users from a specified role." + Requirements)]
        public async Task RoleUnban(IMessage message, IRole role, params IGuildUser[] users) {
            var guildConfig = Config.GetGuildConfig(Check.InGuild(message).Guild);
            var action = await CommandUtility.Action(message, "ban",
                u => {
                    guildConfig.GetUserConfig(u).UnbanRole(role);
                    return Task.CompletedTask;
                });
            await CommandUtility.ForEvery(message, users, action);
        }

        [Command("role create")]
        [Permission(GuildPermission.ManageRoles)]
        [Description("Creates a mentionable role and applies it to all mentioned users")]
        public async Task RoleCreate(IMessage message, string name) {
            var guild = Check.InGuild(message).Guild;
            await guild.CreateRoleAsync(name);
            await message.Success();
        }

        [Command("role delete")]
        [Permission(GuildPermission.ManageRoles)]
        [Description("Deletes a role and removes it from all users.")]
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
                throw new RoleRankException($"{m.Author.Username}, you cannot {action} role \"{role.Name}\", as it is above their roles.");
            await CommandUtility.ForEvery(m, users,
                await CommandUtility.Action(m, action + " role", user => task(user, role)));
        }
    }
}
