using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace DrumBot {


    [Module]
    public class Standard {

        [Command("avatar")]
        [Description("Gets the avatar url of all mentioned users.")]
        public async Task Avatar(IMessage message, params IGuildUser[] users) {
            IUser[] allUsers = users;
            if (users.Length <= 0)
                allUsers = new[] {message.Author};
            await message.Respond(allUsers.Select(u => u.AvatarUrl).Join("\n"));
        }

        [Command("serverinfo")]
        [Description("Gets general information about the current server")]
        public async Task ServerInfo(IMessage message) {
            var builder = new StringBuilder();
            var server = Check.InGuild(message).Guild;
            var config = Config.GetGuildConfig(server);
            var owner = await server.GetUserAsync(server.OwnerId);
            var channels = await server.GetChannelsAsync();
            var textChannels = channels.OfType<ITextChannel>().Order().Select(ch => ch.Name);
            var voiceChannels = channels.OfType<IVoiceChannel>().Order().Select(ch => ch.Name);
            using (builder.MultilineCode()) {
                builder.AppendLine($"Name: {server.Name}");
                builder.AppendLine($"ID: {server.Id}");
                builder.AppendLine($"Owner: {owner.Username}");
                builder.AppendLine($"Region: {server.VoiceRegionId}");
                builder.AppendLine($"User Count: {server.GetCachedUserCount()}");
                builder.AppendLine($"Roles: {server.Roles.Order().Select(r => r.Name).Join(", ")}");
                builder.AppendLine($"Text Channels: {textChannels.Join(", ")}");
                builder.AppendLine($"Voice Channels: {voiceChannels.Join(", ")}");
                builder.AppendLine($"Server Type: {config.Type}");
            }
            if(!string.IsNullOrEmpty(server.IconUrl))
                builder.AppendLine(server.IconUrl);
            await message.Channel.SendMessageAsync(builder.ToString());
        }

        [Command("whois")]
        [Description("Gets information on a specified users")]
        public async Task WhoIs(IMessage message, IGuildUser user) {
            var builder = new StringBuilder();
            builder.AppendLine($"{message.Author.Mention}:");
            using (builder.MultilineCode()) {
                builder.AppendLine($"Username: {user.Username} {(user.IsBot ? "(BOT)" : string.Empty )}");
                builder.AppendLine($"Nickname: {user.Nickname.NullIfEmpty() ?? "N/A"}");
                builder.AppendLine($"Current Game: {user.Game?.Name ?? "N/A"}");
                builder.AppendLine($"ID: {user.Id}");
                builder.AppendLine($"Joined on: {user.JoinedAt?.ToString() ?? "N/A"}");
                builder.AppendLine($"Created on: {user.CreatedAt}");
                builder.AppendLine($"Roles: {user.Roles.Where(r => r != user.Guild.EveryoneRole).Select(r => r.Name).Join(", ")}");
            }
            if(!string.IsNullOrEmpty(user.AvatarUrl))
                builder.AppendLine(user.AvatarUrl);
            await message.Channel.SendMessageAsync(builder.ToString());
        }
    }
}
