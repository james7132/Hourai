using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace DrumBot {
    public static class DiscordExtensions {

        public static async Task Respond(this CommandEventArgs evt, string response) {
            if (response.Length > DiscordConfig.MaxMessageSize) {
                using (var stream = new MemoryStream()) {
                    var writer = new StreamWriter(stream);
                    writer.Write(response);
                    writer.Flush();
                    stream.Position = 0;
                    await evt.Channel.SendFile("results.txt", stream);
                }
            }
            else {
                if(response.Length > 0)
                    await evt.Channel.SendMessage(response);
            }
        }

        public static string ToProcessedString(this Message message) => $"{message.User?.Name ?? "Unknown User"}: {message.Text}";
        public static ServerConfig GetConfig(this Server server) => Bot.Config.GetServerConfig(server);

        public static int CompareTo(this User u1, User u2) {
            Func<Role, int> rolePos = role => role.Position;
            return u1.Roles.Max(rolePos).CompareTo(u2.Roles.Max(rolePos));
        } 

    }
}
