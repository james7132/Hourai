using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Commands.Permissions;

namespace DrumBot {
    public class AdminService : IService {

        public void Install(DiscordClient client) {
            var commandService = client.GetService<CommandService>();

            CreateCommand(commandService, "kick",
                "Kicks all mentioned users." + Requires("Kick"),
                Check.KickMembers(),
                user => user.Kick());
            CreateCommand(commandService, "ban",
                "Kicks all mentioned users." + Requires("Ban"),
                Check.BanMembers(),
                UserExtensions.Ban);
            CreateCommand(commandService, "mute", 
                "Server mutes all mentioned users." + Requires("Mute"),
                Check.MuteMembers(),
                UserExtensions.Mute);
            CreateCommand(commandService, "unmute", 
                "Server unmutes all mentioned users" + Requires("Mute"),
                Check.MuteMembers(),
                UserExtensions.Unmute, true);
            CreateCommand(commandService, "deafen", 
                "Server deafens all mentioned users." + Requires("Deafen"),
                Check.DeafenMembers(),
                UserExtensions.Deafen, true);
            CreateCommand(commandService, "undeafen", 
                "Server ukdeafens all mentioned users." + Requires("Deafen"),
                Check.DeafenMembers(),
                UserExtensions.Undeafen, true);
        }

        string Requires(string permission) {
            return $" Requires ``{permission} Members`` permission for both user and bot.";
        }

        void CreateCommand(CommandService service, 
                           string name, 
                           string description, 
                           IPermissionChecker check,
                           Func<User, Task> action, 
                           bool ignorErrors = false) {
            service.CreateCommand(name).Description(description)
                   .Parameter("User(s)", ParameterType.Multiple)
                   .AddCheck(new ProdChecker())
                   .AddCheck(check)
                   .Do(async e => {
                       await Command.ForEvery(e, e.Message.MentionedUsers, 
                            Command.Action(e.Channel, name, action, ignoreErrors: ignorErrors));
                   });
        }
    }
}
