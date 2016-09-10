using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace DrumBot {

    [Module]
    [PublicOnly]
    [ModuleCheck]
    public class Admin {

        [Command("kick")]
        [Permission(GuildPermission.KickMembers)]
        [Description( "Kicks all mentioned users. Requires ``Kick Members`` permission.")]
        public async Task Kick(IUserMessage msg, 
             params IGuildUser[] users) {
            var action = await CommandUtility.Action(msg, "kick", async u => await u.KickAsync());
            await CommandUtility.ForEvery(msg, users, action);
        }

        [Command("ban")]
        [Permission(GuildPermission.BanMembers)]
        [Description( "Bans all mentioned users. Requires ``Ban Members`` permission.")]
        public async Task Ban(IUserMessage msg, 
             params IGuildUser[] users) {
            var action = await CommandUtility.Action(msg, "ban", async u => await u.BanAsync());
            await CommandUtility.ForEvery(msg, users, action);
        }

        [Command("mute")]
        [Permission(GuildPermission.MuteMembers)]
        [Description( "Server mutes all mentioned users. Requires ``Mute Members`` permission.")]
        public async Task Mute(IUserMessage msg, params IGuildUser[] users) {
            var action = await CommandUtility.Action(msg, "mute", async u => await u.MuteAsync());
            await CommandUtility.ForEvery(msg, users, action);
        }

        [Command("unmute")]
        [Permission(GuildPermission.MuteMembers)]
        [Description( "Server unmutes all mentioned users. Requires ``Mute Members`` permission.")]
        public async Task Unmute(IUserMessage msg, params IGuildUser[] users) {
            var action = await CommandUtility.Action(msg, "unmute", async u => await u.UnmuteAsync());
            await CommandUtility.ForEvery(msg, users, action);
        }

        [Command("deafen")]
        [Permission(GuildPermission.DeafenMembers)]
        [Description( "Server deafens all mentioned users. Requires ``Deafen Members`` permission.")]
        public async Task Deafen(IUserMessage msg, params IGuildUser[] users) {
            var action = await CommandUtility.Action(msg, "deafen", async u => await u.DeafenAsync());
            await CommandUtility.ForEvery(msg, users, action);
        }

        [Command("undeafen")]
        [Permission(GuildPermission.DeafenMembers)]
        [Description( "Server undeafens all mentioned users. Requires ``Deafen Members`` permission.")]
        public async Task Undeafen(IUserMessage msg, params IGuildUser[] users) {
            var action = await CommandUtility.Action(msg, "undeafen", async u => await u.UndeafenAsync());
            await CommandUtility.ForEvery(msg, users, action);
        }

        [Command("nickname")]
        [Description("Sets the nickname of all mentioned users, or nicknames yourself.\nIf no ``users`` is empty, nicknames the user who used the command"
            + "and requires the ``Change Nickname`` permission.\nIf at least one ``user`` is specified, nicknames the mentioned users and requires the "
            + "``Manage Nicknames`` permission.")]
        public async Task Nickname(IUserMessage msg, string nickname, params IGuildUser[] users) {
            Check.InGuild(msg);
            var author = msg.Author as IGuildUser;
            IGuildUser[] allUsers = users;
            if (allUsers.Length <= 0) {
                if(!author.GuildPermissions.ChangeNickname) {
                    await msg.Respond($"{author.Mention} you do not have the ``Change Nickname`` permission. See ``{Config.CommandPrefix}help nickname``");
                    return;
                }
                allUsers = new[] {msg.Author as IGuildUser};
            }
            if(!author.GuildPermissions.ManageNicknames) {
                await msg.Respond($"{author.Mention} you do not have the ``Manage Nicknames`` permission. See ``{Config.CommandPrefix}help nickname``");
                return;
            }

            var action = await CommandUtility.Action(msg, "nickname", async u => await u.SetNickname(nickname));
            await CommandUtility.ForEvery(msg, allUsers, action);
        }

        [Group("prune")]
        public class PruneGroup {

            [Command]
            [Permission(GuildPermission.ManageMessages)]
            [Description("Removes the last X messages from the current channel. Requires ``Manage Messages`` permission.")]
            public Task Prune(IUserMessage msg, int count = 100) {
                return PruneMessages(Check.InGuild(msg), m => true, count);
            }

            [Command("user")]
            [Permission(GuildPermission.ManageMessages)]
            [Description("Removes all messages from all mentioned users in the last 100 messages. Requires ``Manage Messages`` permission.")]
            public Task PruneUser(IUserMessage msg, params IGuildUser[] users) {
                var userSet = new HashSet<IUser>(users);
                return PruneMessages(Check.InGuild(msg), m => userSet.Contains(m.Author));
            }

            [Command("embed")]
            [Permission(GuildPermission.ManageMessages)]
            [Description("Removes all messages with embeds or attachments in the last X messages. Requires ``Manage Messages`` permission.")]
            public Task PruneEmbed(IUserMessage msg, int count = 100) {
                return PruneMessages(Check.InGuild(msg), m => m.Embeds.Any() || m.Attachments.Any(), count);
            }

            [Command("mine")]
            [Description("Removes all messages from the user using the command in the last X messages.")]
            public Task PruneMine(IUserMessage msg, int count = 100) {
                ulong id = msg.Author.Id;
                return PruneMessages(Check.InGuild(msg), m => m.Author.Id == id, count);
            }

            [Command("bot")]
            [Permission(GuildPermission.ManageMessages)]
            [Description("Removes all messages from all bots in the last X messages. Requires ``Manage Messages`` permission.")]
            public Task PruneBot(IUserMessage msg, int count = 100) {
                return PruneMessages(Check.InGuild(msg), m => m.Author.IsBot, count);
            }

            static async Task PruneMessages(IMessageChannel channel,
                                            Func<IMessage, bool> pred = null,
                                            int count = 100) {
                if (count > Config.PruneLimit)
                    count = Config.PruneLimit;
                if (count < 0) {
                    await channel.Respond("Cannot prune a negative count of messages");
                    return;
                }
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
                await channel.Success($"Deleted {finalCount} messages.");
            }
        }

        [Group("channel")]
        public class ChannelGroup {

            [Command("create")]
            [Permission(GuildPermission.ManageChannels)]
            [Description("Creates a public channel with a specified name. Requires ``Manage Channels`` permission.")]
            public async Task ChannelCreate(IUserMessage msg, string name) {
                var guild = Check.InGuild(msg).Guild;
                var channel = await guild.CreateTextChannelAsync(name); 
                await msg.Success($"{channel.Mention} created.");
            }

            [Command("delete")]
            [Permission(GuildPermission.ManageChannels)]
            [Description("Deletes all mentioned channels. Requires ``Manage Channels`` permission.")]
            public Task ChannelDelete(IUserMessage msg, params IGuildChannel[] channels) {
                return CommandUtility.ForEvery(msg, channels, CommandUtility.Action(
                    async delegate(IGuildChannel channel) {
                        await channel.DeleteAsync();
                    }));
            }

        }

        [Group("role")]
        public class RoleGroup {
            const string Requirement = " Requires ``Manage Role`` permission for both user and bot.";

            [Command("add")]
            [Permission(GuildPermission.ManageRoles)]
            [Description("Adds a role to all mentioned users." + Requirement)]
            public async Task Add(IUserMessage msg, IRole role, params IGuildUser[] users) {
                var action = await CommandUtility.Action(msg, "add role", async u => await u.AddRolesAsync(role));
                await CommandUtility.ForEvery(msg, users, action);
            }
            
            [Command("remove")]
            [Permission(GuildPermission.ManageRoles)]
            [Description("Removes a role to all mentioned users." + Requirement)]
            public async Task Remove(IUserMessage msg, IRole role, params IGuildUser[] users) {
                var action = await CommandUtility.Action(msg, "remove role", async u => await u.RemoveRolesAsync(role));
                await CommandUtility.ForEvery(msg, users, action);
            }

            [Command("nuke")]
            [Permission(GuildPermission.ManageRoles)]
            [Description("Removes a role to all users on the server." + Requirement)]
            public async Task Nuke(IUserMessage msg, params IRole[] roles) {
                var users = await Check.InGuild(msg).Guild.GetUsersAsync();
                var action = await CommandUtility.Action(msg, "remove role", async u => await u.RemoveRolesAsync(roles));
                await CommandUtility.ForEvery(msg, users, action);
            }

            [Command("ban")]
            [Permission(GuildPermission.ManageRoles)]
            [Description("Bans all mentioned users from a specified role." + Requirement)]
            public async Task RoleBan(IUserMessage msg, IRole role, params IGuildUser[] users) {
                var guildConfig = Config.GetGuildConfig(Check.InGuild(msg).Guild);
                var action = await CommandUtility.Action(msg, "ban",
                    async u => {
                        await u.RemoveRolesAsync(role);
                        guildConfig.GetUserConfig(u).BanRole(role);
                    });
                await CommandUtility.ForEvery(msg, users, action);
            }

            [Command("unban")]
            [Permission(GuildPermission.ManageRoles)]
            [Description("Unban all mentioned users from a specified role." + Requirement)]
            public async Task RoleUnban(IUserMessage msg, IRole role, params IGuildUser[] users) {
                var guildConfig = Config.GetGuildConfig(Check.InGuild(msg).Guild);
                var action = await CommandUtility.Action(msg, "ban",
                    u => {
                        guildConfig.GetUserConfig(u).UnbanRole(role);
                        return Task.CompletedTask;
                    });
                await CommandUtility.ForEvery(msg, users, action);
            }

            [Command("create")]
            [Permission(GuildPermission.ManageRoles)]
            [Description("Creates a mentionable role and applies it to all mentioned users")]
            public async Task RoleCreate(IUserMessage msg, string name) {
                var guild = Check.InGuild(msg).Guild;
                await guild.CreateRoleAsync(name);
                await msg.Success();
            }

            [Command("delete")]
            [Permission(GuildPermission.ManageRoles)]
            [Description("Deletes a role and removes it from all users.")]
            public async Task RoleDelete(IUserMessage msg, params IRole[] roles) {
                await CommandUtility.ForEvery(msg, roles, CommandUtility.Action(
                    async delegate(IRole role) {
                        await role.DeleteAsync(); 
                    }));
            }
        }

        static async Task RoleCommand(IUserMessage m, IRole role, string action, IEnumerable<IGuildUser> users, Func<IGuildUser, IRole, Task> task) {
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
