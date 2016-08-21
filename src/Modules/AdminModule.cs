using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Commands.Permissions.Visibility;
using Discord.Modules;
using Discord.Net;

namespace DrumBot {
    public class AdminModule : IModule {

        public void Install(ModuleManager manager) {

            manager.UserUpdated +=
                async delegate(object s, UserUpdatedEventArgs u) {
                    if (!u.Server.CurrentUser.ServerPermissions.ManageNicknames || u.After.Nickname == u.Before.Nickname)
                        return;
                    try {
                        if (Config.GetServerConfig(u.Server)
                                .GetUserConfig(u.After)
                                .IsNicknameLocked)
                            await u.After.SetNickname(u.After.Name);
                    } catch (HttpException e) {
                        if(e.StatusCode != HttpStatusCode.BadGateway)
                            Log.Error(e);
                    }
                };

            manager.CreateCommands(cbg => {
                cbg.PublicOnly();
                CreateCommand(cbg, "kick", "Kicks all mentioned users.")
                    .AddCheck(Check.KickMembers())
                    .Do(AdminAction("kick", async u => await u.Kick()));
                CreateCommand(cbg, "ban", "Bans all mentioned users.")
                    .AddCheck(Check.KickMembers())
                    .Do(AdminAction("ban", async u => await u.Ban()));
                cbg.CreateGroup(m => {
                    m.AddCheck(Check.MuteMembers());
                    CreateCommand(m, "mute", "Server mutes all mentioned users.")
                        .Do(AdminAction("mute", async u => await u.Mute(), true));
                    CreateCommand(m, "unmute", "Server unmutes all mentioned users.")
                        .Do(AdminAction("unmute", async u => await u.Unmute(), true));
                });
                cbg.CreateGroup(d => {
                    d.AddCheck(Check.DeafenMembers());
                    CreateCommand(d, "deafen", "Server deafens all mentioned users.")
                        .Do(AdminAction("deafen", async u => await u.Deafen(), true));
                    CreateCommand(d, "undeafen", "Server undeafens all mentioned users.")
                        .Do(AdminAction("undeafen", async u => await u.Undeafen(), true));
                });
                cbg.CreateGroup("nickname", n => {
                    CreateCommand(n, "", "", "Nickname")
                        .Description("Sets the nickname of all mentioned users. If none are mentioned, will change yours instead"
                            + Utility.Requires("Manage/Change Nicknames"))
                        .AddCheck((co, u, ch) => u.ServerPermissions.ChangeNickname || u.ServerPermissions.ManageNicknames)
                        .Do(async e => {
                            string nickname = e.GetArg("Nickname");
                            IEnumerable<User> users = e.Message.MentionedUsers;
                            if(!e.Message.MentionedUsers.Any()) {
                                users = new[] {e.Message.User};
                            } else if (!e.User.ServerPermissions.ManageNicknames) {
                                await e.Respond($"{e.User.Mention}: you do not have the permission to change other people's nicknames.");
                                return;
                            }
                            await Command.ForEvery(e, users,
                                Command.Action(e.Server, "nickname", async u => await u.SetNickname(nickname), true));
                        });
                    CreateCommand(n, "lock", "", "Nickname")
                        .Description("Prevents users from changing their nicknames."
                            + Utility.Requires("Manage/Change Nicknames"))
                        .AddCheck((co, u, ch) => u.ServerPermissions.ChangeNickname || u.ServerPermissions.ManageNicknames)
                        .Do(async e => {
                            var config = Config.GetServerConfig(e.Server);
                            var userConfigs = e.Message.MentionedUsers.Select(u => config.GetUserConfig(u));
                            foreach (UserConfig userConfig in userConfigs)
                                userConfig.LockNickname();
                            await Task.WhenAll(e.Message.MentionedUsers.Select(u => u.SetNickname(u.Name)));
                            await e.Success();
                        });
                    CreateCommand(n, "unlock", "", "Nickname")
                        .Description("Prevents users from changing their nicknames."
                            + Utility.Requires("Manage/Change Nicknames"))
                        .AddCheck((co, u, ch) => u.ServerPermissions.ChangeNickname || u.ServerPermissions.ManageNicknames)
                        .Do(async e => {
                            var config = Config.GetServerConfig(e.Server);
                            var userConfigs = e.Message.MentionedUsers.Select(u => config.GetUserConfig(u));
                            foreach (UserConfig userConfig in userConfigs)
                                userConfig.UnlockNickname();
                            await e.Success();
                        });
                    });

                cbg.CreateGroup("prune",
                    p => {
                        p.AddCheck(Check.ManageMessages());
                        p.CreateCommand()
                            .Description("Removes the last X messages from the current channel." + Utility.Requires("Manage Messages"))
                            .Parameter("Count")
                            .Do(Command.Response(async e => await Prune(e)));
                        p.CreateCommand("user")
                         .Description("Removes all messages from all mentioned users in the last 100 messages." + Utility.Requires("Manage Messages"))
                         .Parameter("User(s)", ParameterType.Multiple)
                         .Do(Command.Response(async e => await PruneMessages(e.Channel, 100, m => e.Message.MentionedUsers.Contains(m.User))));
                        p.CreateCommand("embed")
                         .Description("Removes all messages with embeds in the last 100 messages. " + Utility.Requires("Manage Messages"))
                         .Do(Command.Response(async e => await PruneMessages(e.Channel, 100, m => m.Embeds.Any() || m.Attachments.Any())));
                    });
            });
        }

        Func<CommandEventArgs, Task> AdminAction(string action,
                                                 Func<User, Task> task,
                                                 bool ignore = false) {
            return async delegate(CommandEventArgs e) {
                    await Command.ForEvery(e, e.Message.MentionedUsers,
                       Command.Action(e.Server, action, task, ignore));
                };
        }

        CommandBuilder CreateCommand(CommandGroupBuilder builder,
                                     string name,
                                     string description,
                                     params string[] parameters) {
            name = name.ToLowerInvariant();
            var command = builder.CreateCommand(name)
                                 .Description(description + Utility.RequireMembers(name.Replace("un", "").ToTitleCase()));
            foreach (var parameter in parameters)
                command.Parameter(parameter);
            command.Parameter("Users(s)", ParameterType.Multiple);
            return command;
        }

        static async Task<string> PruneMessages(Channel channel,
                                        int count,
                                        Func<Message, bool> pred) {
            if (count > Config.PruneLimit)
                count = Config.PruneLimit;
            if (count < 0)
                return "Cannot prune a negative count of messages";
            var messages = await channel.DownloadMessages(count);
            var finalCount = Math.Min(messages.Length, count);
            await channel.DeleteMessages(messages.Where(pred)
                                        .OrderByDescending(m => m.Timestamp)
                                        .Take(count)
                                        .ToArray());
            return Utility.Success($"Deleted {finalCount} messages.");
        }

        async Task<string> Prune(CommandEventArgs e) {
            int count;
            var countArg = e.GetArg("Count");
            if (!int.TryParse(countArg, out count)) 
                return $"Prune failure. Cannot parse {countArg.Code()} to a valid value.";
            return await PruneMessages(e.Channel, count, m => true);
        }
    }
}
