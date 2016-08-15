using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord;

namespace DrumBot {
    public static class DiscordExtensions {

        public static IEnumerable<T> EmptyIfNull<T>(this IEnumerable<T> enumerable) {
            return enumerable ?? Enumerable.Empty<T>();
        }

        public static TValue GetValueOrDefault<TKey, TValue>(
            this Dictionary<TKey, TValue> dict,
            TKey key) {
            return dict.ContainsKey(key) ? dict[key] : default(TValue);
        }

        public static Task<T> ToTask<T>(this T obj) {
            return Task.FromResult(obj);
        }

        /// <summary>
        /// Creates a response to a command. If the result is larger than can be retured as a single message, 
        /// will upload as a text file.
        /// </summary>
        /// <param name="message">the message to respond to</param>
        /// <param name="response">the string of the message to respond with.</param>
        public static Task Respond(this IMessage message, string response) =>
            message.Channel.Respond(response);

        static readonly Random Random = new Random();

        public static T SelectRandom<T>(this IEnumerable<T> t) {
            var array = t.ToArray();
            return array[Random.Next(array.Length)];
        }

        public static Task Respond(this IMessageChannel channel, string response) {
            const string fileName = "results.txt";
            if (response.Length > DiscordConfig.MaxMessageSize)
                return channel.SendFileAsync(fileName, response);
            if(response.Length > 0)
                return channel.SendMessageAsync(response);
            return Task.CompletedTask;
        }

        public static Task Success(this IMessage message,
                                        string followup = null) => 
            message.Channel.Success(followup);

        public static Task Success(this IMessageChannel channel, string followup) =>
            channel.Respond(Utility.Success(followup));

        public static Task SendFileRetry(this IMessageChannel user,
                                              string path,
                                              string text = null) {
            return Utility.FileIO(async () => {
                using (var file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                    await user.SendFileAsync(file, Path.GetFileName(path), text);
                }
            });
        }

        public static Task SendFileRetry(this IMessage message, string path, string text = null) => 
            message.Channel.SendFileRetry(path, text);

        public static async Task SendMemoryFile(this IMessageChannel channel,
                                                  string name,
                                                  string value,
                                                  string text = null) {
            using (var stream = new MemoryStream()) {
                var writer = new StreamWriter(stream);
                writer.Write(value);
                writer.Flush();
                stream.Position = 0;
                await channel.SendFileAsync(stream, name, text);
            }
        }

        public static string ToProcessedString(this IMessage message) {
            var baseLog = $"{message.Author?.Username ?? "Unknown User"}: {message.Content}";
            var attachments = message.Attachments.Select(a => a.Url).Join(" ");
            var embeds = message.Embeds.Select(a => a.Url).Join(" ");
            return baseLog + attachments + embeds;
        }

        /// <summary>
        /// Compares two users. Favors the channel with the higher highest role.
        /// </summary>
        public static int CompareTo(this IGuildUser u1, IGuildUser u2) {
            Func<IRole, int> rolePos = role => role.Position;
            return u1.Roles.Max(rolePos).CompareTo(u2.Roles.Max(rolePos));
        } 

    }
}
