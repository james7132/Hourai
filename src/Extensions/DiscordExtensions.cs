using System;
using System.Collections.Generic;
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
            await evt.Channel.Respond(response);
        }

        static readonly Random Random = new Random();

        public static T SelectRandom<T>(this IEnumerable<T> t) {
            var array = t.ToArray();
            return array[Random.Next(array.Length)];
        }

        public static async Task Respond(this Channel channel, string response) {
            const string fileName = "results.txt";
            if (response.Length > DiscordConfig.MaxMessageSize) {
                if (channel.IsPrivate)
                    await channel.Users.FirstOrDefault().SendMemoryFile(fileName, response);
                else
                    await channel.SendMemoryFile(fileName, response);
            }
            else {
                if(response.Length > 0)
                    await channel.SendMessage(response);
            }
        }

        public static async Task Success(this CommandEventArgs e,
                                         string followup = null) {
            await e.Channel.Success(followup);
        } 

        public static DateTime CreatedOn(this User user) {
            ulong id = Check.NotNull(user).Id;
            // Date created is bits 23 to 64
            id >>= 22;
            // Add offset to Jan. 1st 2016
            id += 1420070400000;
            // Convert from Unix Timestamp to DateTime
            var dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            return dtDateTime.AddMilliseconds(id);
        }

        public static async Task Success(this Channel channel, string followup) {
            await channel.Respond(Utility.Success(followup));
        }

        public static async Task SendFileRetry(this Channel user,
                                                string path) {
            await Utility.FileIO(async () => {
                using (var file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                    await user.SendFile(Path.GetFileName(path), file);
                }
            });
        }

        public static async Task SendFileRetry(this User user,
                                               string path) {
            await Utility.FileIO(async () => {
                using (var file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                    await user.SendFile(Path.GetFileName(path), file);
                }
            });
        }

        public static async Task SendMemoryFile(this Channel channel,
                                          string name,
                                          string value) {
            using (var stream = new MemoryStream()) {
                var writer = new StreamWriter(stream);
                writer.Write(value);
                writer.Flush();
                stream.Position = 0;
                await channel.SendFile(name, stream);
            }
        }

        public static async Task SendMemoryFile(this User user,
                                          string name,
                                          string value) {
            using (var stream = new MemoryStream()) {
                var writer = new StreamWriter(stream);
                writer.Write(value);
                writer.Flush();
                stream.Position = 0;
                await user.SendFile(name, stream);
            }
        }


        public static string ToProcessedString(this Message message) {
            var baseLog = $"{message.User?.Name ?? "Unknown User"}: {message.Text}";
            var attachments = message.Attachments.Select(a => a.Url).Join(" ");
            var embeds = message.Embeds.Select(a => a.Url).Join(" ");
            return baseLog + attachments + embeds;
        }

        /// <summary>
        /// Compares two users. Favors the channel with the higher highest role.
        /// </summary>
        public static int CompareTo(this User u1, User u2) {
            Func<Role, int> rolePos = role => role.Position;
            return u1.Roles.Max(rolePos).CompareTo(u2.Roles.Max(rolePos));
        } 

    }
}
