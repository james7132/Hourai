using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Commands.Permissions;
using Discord.Modules;
using DrumBot.src;

namespace DrumBot {
    public class AdminModule : IModule {

        public void Install(ModuleManager manager) {
            manager.CreateCommands(cbg => {
                CreateCommand(cbg,
                    "kick",
                    "Kicks all mentioned users." + RequireMembers("Kick"),
                    (e, u) => u.Kick()).AddCheck(Check.KickMembers());
                CreateCommand(cbg, "ban",
                    "Kicks all mentioned users." + RequireMembers("Ban"),
                    (e, u) => u.Ban()).AddCheck(Check.BanMembers());
                cbg.CreateGroup(m => {
                    m.AddCheck(Check.MuteMembers());
                    CreateCommand(cbg, "mute", 
                        "Server mutes all mentioned users." + RequireMembers("Mute"),
                        (e, u) => u.Mute(), true);
                    CreateCommand(cbg, "unmute", 
                        "Server unmutes all mentioned users" + RequireMembers("Mute"),
                        (e, u) => u.Unmute(), true);
                });
                cbg.CreateGroup(d => {
                    d.AddCheck(Check.DeafenMembers());
                    CreateCommand(d, "deafen", 
                        "Server deafens all mentioned users." + RequireMembers("Deafen"),
                        (e, u) => u.Deafen(), true);
                    CreateCommand(d, "undeafen", 
                        "Server ukdeafens all mentioned users." + RequireMembers("Deafen"),
                        (e, u) => u.Undeafen(), true);
                });
                CreateCommand(cbg,
                    "nickname",
                    "Sets the nickname of all mentioned users."
                        + Require("Manage Nicknames"),
                    (e, u) => u.SetNickname(e.GetArg("Nickname")),
                    true, "Nickname")
                    .AddCheck(Check.ManageNicknames());
                cbg.CreateGroup("prune", p => {
                    p.AddCheck(Check.ManageMessages());
                    p.CreateCommand()
                        .Description("Removes the last X messages from the current channel.")
                        .Parameter("Message Count")
                        .Do(Prune);
                    p.CreateCommand("user")
                        .Description("Removes all messages from all mentioned users in the last 100 messages.")
                        .Do(async e => await PruneMessages(e.Channel, 100, m => e.Message.MentionedUsers.Contains(m.User)));
                });
            });
        }

        static async Task PruneMessages(Channel channel, int count, Func<Message, bool> pred) { 
            if (count > Config.PruneLimit)
                count = Config.PruneLimit;
            var messages = await channel.DownloadMessages(count);
            var finalCount = Math.Min(messages.Length, count);
            await channel.DeleteMessages(messages.Where(pred).OrderByDescending(m => m.Timestamp).Take(count).ToArray());
            await channel.Respond(Config.SuccessResponse + $" Deleted { finalCount } messages.");
        }

        async Task Prune(CommandEventArgs e) {
            int count;
            var countArg = e.GetArg("Message Count");
            if (!int.TryParse(countArg, out count)) {
                await e.Respond($"Prune failure. Cannot parse {countArg} to a valid value.");
                return;
            }
            if (count < 0) {
                await e.Respond("Cannot a negative count of messages");
                return;
            }
            await PruneMessages(e.Channel, count, m => true);
        }

        string Require(string permission) {
            return $" Requires ``{permission}`` permission for both user and bot.";
        }

        string RequireMembers(string permission) {
            return Require(permission + " Members");
        }

        Func<CommandEventArgs, Task> ActionCommand(
            Func<CommandEventArgs, IEnumerable<IAction>>  actionFunc) {
            return async delegate (CommandEventArgs e) {
                await actionFunc(e).Do(e.Server);
            };
        }

        CommandBuilder CreateCommand(CommandGroupBuilder builder, 
                           string name, 
                           string description, 
                           Func<CommandEventArgs, User, Task> action, 
                           bool ignorErrors = false, params string[] parameters) {
            var command = builder.CreateCommand(name).Description(description);
            foreach (string parameter in parameters) 
                command = command.Parameter(parameter);
            command.Parameter("User(s)", ParameterType.Multiple);
            command.Do(async e => {
                       await Command.ForEvery(e, e.Message.MentionedUsers, 
                            Command.Action(e.Channel, name, async u => await action(e, u), ignoreErrors: ignorErrors));
                   });
            return command;
        }
    }
}
