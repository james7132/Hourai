using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace DrumBot {
    public class InfoService : IService {

        async void Info(CommandEventArgs e) {
            var builder = new StringBuilder();
            var server = e.Server;
            var config = Config.GetServerConfig(e.Server);
            using (builder.MultilineCode()) {
                builder.AppendLine($"Name: {server.Name}");
                builder.AppendLine($"ID: { server.Id }");
                builder.AppendLine($"Owner: { server.Owner.Name }");
                builder.AppendLine($"Region: { server.Region.Name }");
                builder.AppendLine($"User Count: {server.UserCount}");
                builder.AppendLine($"Roles: { string.Join(", ", server.Roles.Order().Select(r => r.Name))}");
                builder.AppendLine($"Text Channels: { string.Join(", ", server.TextChannels.Order().Select(ch => ch.Name))}");
                builder.AppendLine($"Voice Channels: { string.Join(", ", server.VoiceChannels.Order().Select(ch => ch.Name))}");
                builder.AppendLine($"Server Type: { config.Type }");
            }
            if(!string.IsNullOrEmpty(server.IconUrl))
                builder.AppendLine(server.IconUrl);
            await e.Respond(builder.ToString());
        }

        static async Task Avatar(CommandEventArgs e) {
            await Command.ForEvery(e, e.Message.MentionedUsers,
                async user => $"{user.Name}: {user.AvatarUrl}");
        }

        async Task WhoIs(CommandEventArgs e) {
            var targetUser = e.Message.MentionedUsers.FirstOrDefault();
            if(targetUser == null)
                throw new Exception("No user mentioned.");
            var builder = new StringBuilder();
            builder.AppendLine($"{e.User.Mention}");
            using (builder.MultilineCode()) {
                builder.AppendLine($"Username: {targetUser.Name}{(targetUser.IsBot ? " (BOT)" : string.Empty )}");
                builder.AppendLine($"Nickname: { (string.IsNullOrEmpty(targetUser.Nickname) ? "N/A" : targetUser.Nickname )}");
                builder.AppendLine($"Current Game: {targetUser.CurrentGame?.Name ?? "N/A"}");
                builder.AppendLine($"ID: {targetUser.Id}");
                builder.AppendLine($"Joined on: { targetUser.JoinedAt }");
                builder.AppendLine($"Last Activity: { targetUser.LastActivityAt?.ToString() ?? "N/A" }");
                builder.AppendLine($"Last Online: { targetUser.LastOnlineAt?.ToString() ?? "N/A" }");
                builder.AppendLine($"Roles: { string.Join(", ", targetUser.Roles.Where(r => r != e.Server.EveryoneRole).Select(r => r.Name)) }");
            }
            if(!string.IsNullOrEmpty(targetUser.AvatarUrl))
                builder.AppendLine(targetUser.AvatarUrl);
            await e.Respond(builder.ToString());
        }

        public void Install(DiscordClient client) {
            var commandService = client.GetService<CommandService>();

            commandService.CreateCommand("avatar")
                .Description("Gets the avatar url of all mentioned users.")
                .Parameter("User(s)", ParameterType.Multiple)
                .AddCheck(new ProdChecker())
                .Do(Avatar);

            commandService.CreateCommand("whois")
                .Description("Gets information on a specified user")
                .Parameter("User")
                .AddCheck(new ProdChecker())
                .Do(WhoIs);
            
            commandService.CreateGroup("server", cbg => {
                cbg.CreateCommand("info")
                   .Description("Gets general information about the current server")
                   .AddCheck(new ProdChecker())
                   .Do(e => Info(e));
            });
        }
    }
}
