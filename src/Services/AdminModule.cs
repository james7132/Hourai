using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Commands.Permissions;
using Discord.Modules;

namespace DrumBot {
    public class AdminModule : IModule {

        public void Install(ModuleManager manager) {
            manager.CreateCommands(cbg => {
                cbg.AddCheck(new ProdChecker());
                CreateCommand(cbg, "kick",
                    "Kicks all mentioned users." + Requires("Kick"),
                    user => user.Kick(), false, Check.KickMembers());
                CreateCommand(cbg, "ban",
                    "Kicks all mentioned users." + Requires("Ban"),
                    UserExtensions.Ban, false, Check.BanMembers());
                cbg.CreateGroup(m => {
                    m.AddCheck(Check.MuteMembers());
                    CreateCommand(cbg, "mute", 
                        "Server mutes all mentioned users." + Requires("Mute"),
                        UserExtensions.Mute, true);
                    CreateCommand(cbg, "unmute", 
                        "Server unmutes all mentioned users" + Requires("Mute"),
                        UserExtensions.Unmute, true);
                });
                cbg.CreateGroup(d => {
                    d.AddCheck(Check.DeafenMembers());
                    CreateCommand(d, "deafen", 
                        "Server deafens all mentioned users." + Requires("Deafen"),
                        UserExtensions.Deafen, true);
                    CreateCommand(d, "undeafen", 
                        "Server ukdeafens all mentioned users." + Requires("Deafen"),
                        UserExtensions.Undeafen, true);
                });
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

        string Requires(string permission) {
            return $" Requires ``{permission} Members`` permission for both user and bot.";
        }

        void CreateCommand(CommandGroupBuilder builder, 
                           string name, 
                           string description, 
                           Func<User, Task> action, 
                           bool ignorErrors = false,
                           IPermissionChecker checker = null) {
            var command = builder.CreateCommand(name).Description(description)
                                 .Parameter("User(s)", ParameterType.Multiple);
            if(checker != null)
                command = command.AddCheck(checker);
            command.Do(async e => {
                       await Command.ForEvery(e, e.Message.MentionedUsers, 
                            Command.Action(e.Channel, name, action, ignoreErrors: ignorErrors));
                   });
        }
    }
}
