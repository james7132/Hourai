using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord;

namespace DrumBot {
    public static class DiscordExtensions {

        public static async Task Respond(this Channel channel, string response) {
            if (response.Length > DiscordConfig.MaxMessageSize) {
                using (var stream = new MemoryStream()) {
                    var writer = new StreamWriter(stream);
                    writer.Write(response);
                    writer.Flush();
                    stream.Position = 0;
                    await channel.SendFile("results.txt", stream);
                }
            }
            else {
                await channel.SendMessage(response);
            }
        }

        public static ServerConfig GetConfig(this Server server) {
            return Bot.Config.GetServerConfig(server);
        }

        public static bool IsBotOwner(this User user) {
            return user.Id == Bot.Config.Owner;
        }

        public static bool IsOwner(this User user) {
            return user.Server.Owner == user;
        }

        public static Task AddRoles(this IEnumerable<User> users,
                                                 params Role[] roles) {
            return Task.WhenAll(users.Select(user => user.AddRoles(roles)));
        }

        public static Task RemoveRoles(this IEnumerable<User> users,
                                                    params Role[] roles) {
            return Task.WhenAll(users.Select(user => user.RemoveRoles(roles)));
        }

        public static Task Kick(this IEnumerable<User> users) {
            return Task.WhenAll(users.Select(user => user.Kick()));
        }

        public static Task Ban(this IEnumerable<User> users) {
            return Task.WhenAll(users.Select(user => user.Server.Ban(user)));
        }

    }
}
