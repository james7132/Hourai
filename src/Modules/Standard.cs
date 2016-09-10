using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace DrumBot {

    [Module]
    [ModuleCheck]
    public class Standard {

        [Command("echo")]
        [Remarks("Has the bot repeat what you say")]
        public async Task Echo(IUserMessage message, [Remainder] string remainder) {
            await message.Respond(remainder);
        }

        [Command("avatar")]
        [Remarks("Gets the avatar url of all mentioned users.")]
        public async Task Avatar(IUserMessage message, params IGuildUser[] users) {
            IUser[] allUsers = users;
            if (users.Length <= 0)
                allUsers = new[] {message.Author};
            await message.Respond(allUsers.Select(u => u.AvatarUrl).Join("\n"));
        }

        [Command("serverinfo")]
        [Remarks("Gets general information about the current server")]
        public async Task ServerInfo(IUserMessage message) {
            var builder = new StringBuilder();
            var server = Check.InGuild(message).Guild;
            var config = Config.GetGuildConfig(server);
            var owner = await server.GetUserAsync(server.OwnerId);
            var channels = await server.GetChannelsAsync();
            var textChannels = channels.OfType<ITextChannel>().Order().Select(ch => ch.Name.Code());
            var voiceChannels = channels.OfType<IVoiceChannel>().Order().Select(ch => ch.Name.Code());
            builder.AppendLine($"Name: {server.Name.Code()}");
            builder.AppendLine($"ID: {server.Id.ToString().Code()}");
            builder.AppendLine($"Owner: {owner.Username.Code()}");
            builder.AppendLine($"Region: {server.VoiceRegionId.Code()}");
            builder.AppendLine($"User Count: {server.GetUserCount().ToString().Code()}");
            builder.AppendLine($"Roles: {server.Roles.Where(r => r.Id != server.EveryoneRole.Id).Order().Select(r => r.Name.Code()).Join(", ")}");
            builder.AppendLine($"Text Channels: {textChannels.Join(", ")}");
            builder.AppendLine($"Voice Channels: {voiceChannels.Join(", ")}");
            builder.AppendLine($"Server Type: {config.Type.ToString().Code()}");
            if(!string.IsNullOrEmpty(server.IconUrl))
                builder.AppendLine(server.IconUrl);
            await message.Respond(builder.ToString());
        }

        [Command("whois")]
        [Remarks("Gets information on a specified users")]
        public async Task WhoIs(IUserMessage message, IGuildUser user) {
            var builder = new StringBuilder();
            builder.AppendLine($"{message.Author.Mention}:");
            builder.AppendLine($"Username: {user.Username.Code()} {(user.IsBot ? "(BOT)".Code() : string.Empty )}");
            builder.AppendLine($"Nickname: {user.Nickname.NullIfEmpty()?.Code() ?? "N/A".Code()}");
            builder.AppendLine($"Current Game: {user.Game?.Name.Code() ?? "N/A".Code()}");
            builder.AppendLine($"ID: {user.Id.ToString().Code()}");
            builder.AppendLine($"Joined on: {user.JoinedAt?.ToString().Code() ?? "N/A".Code()}");
            builder.AppendLine($"Created on: {user.CreatedAt.ToString().Code()}");
            builder.AppendLine($"Roles: {user.Roles.Where(r => r.Id != user.Guild.EveryoneRole.Id).Select(r => r.Name.Code()).Join(", ")}");
            if(!string.IsNullOrEmpty(user.AvatarUrl))
                builder.AppendLine(user.AvatarUrl);
            await message.Channel.SendMessageAsync(builder.ToString());
        }
    }
}
