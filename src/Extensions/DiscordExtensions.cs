using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace DrumBot {
    public static class DiscordExtensions {

        /// <summary>
        /// Creates a response to a command. If the result is larger than can be retured as a single message, 
        /// will upload as a text file.
        /// </summary>
        /// <param name="response">the string of the message to respond with.</param>
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

        /// <summary>
        /// Compares two users. Favors the user with the higher highest role.
        /// </summary>
        public static int CompareTo(this User u1, User u2) {
            Func<Role, int> rolePos = role => role.Position;
            return u1.Roles.Max(rolePos).CompareTo(u2.Roles.Max(rolePos));
        } 

    }
}
