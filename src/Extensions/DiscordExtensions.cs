using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Discord;

namespace DrumBot {
    public static class DiscordExtensions {

        public static IEnumerable<T> EmptyIfNull<T>(this IEnumerable<T> enumerable) {
            return enumerable ?? Enumerable.Empty<T>();
        }

        public static IEnumerable<T> MakeUnique<T>(
            this IEnumerable<T> enumerable) {
            var set = new HashSet<T>();
            foreach (T val in enumerable)
                if (set.Add(val))
                    yield return val;
        }

        /// <summary>
        /// Like ToDictionary, but deferred execution and gives no guarantee of uniqueness.
        /// </summary>
        public static IEnumerable<KeyValuePair<TKey, TValue>> ToKVStream<T, TKey, TValue>(this IEnumerable<T> enumerable, 
                                                                                   Func<T, TKey> keyFunc, 
                                                                                   Func<T, TValue> valueFunc) {
            Check.NotNull(keyFunc);
            Check.NotNull(valueFunc);
            foreach (T val in enumerable.EmptyIfNull()) {
                yield return new KeyValuePair<TKey, TValue>(keyFunc(val), valueFunc(val));
            }
        }

        public static Dictionary<TKey, TValue> Evaluate<TKey, TValue>(
            this IEnumerable<KeyValuePair<TKey, TValue>> enumerable) {
            return enumerable.EmptyIfNull().ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        public static Dictionary<T, int> CountValues<T>(this IEnumerable<T> enumerable) {
            var dict = new Dictionary<T, int>();
            foreach (T val in enumerable) {
                if (!dict.ContainsKey(val))
                    dict[val] = 0;
                dict[val]++;
            }
            return dict;
        }

        /// <summary>
        /// Groups values by their key.
        /// </summary>
        public static Dictionary<TKey, List<TValue>> GroupByKey<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> enumerable) {
            var dict = new Dictionary<TKey, List<TValue>>();
            foreach (var keyValuePair in enumerable.EmptyIfNull()) {
                var key = keyValuePair.Key;
                if (!dict.ContainsKey(key))
                    dict[key] = new List<TValue>();
                dict[key].Add(keyValuePair.Value);
            }
            return dict;
        }

        public static IEnumerable<KeyValuePair<TKey, TResult>> MapValue<TKey, TValue, TResult>(
                                                    this IEnumerable<KeyValuePair<TKey, TValue>> enumerable, 
                                                    Func<TValue, TResult> resultFunc) {
            Check.NotNull(resultFunc);
            foreach (var keyValuePair in enumerable.EmptyIfNull())
                yield return new KeyValuePair<TKey, TResult>(keyValuePair.Key, resultFunc(keyValuePair.Value));
        }

        public static IEnumerable<KeyValuePair<TResult, TValue>> MapKey<TKey, TValue, TResult>(
                                                    this IEnumerable<KeyValuePair<TKey, TValue>> enumerable, 
                                                    Func<TKey, TResult> resultFunc) {
            Check.NotNull(resultFunc);
            foreach (var keyValuePair in enumerable.EmptyIfNull())
                yield return new KeyValuePair<TResult, TValue>(resultFunc(keyValuePair.Key), keyValuePair.Value);
        }

        /// <summary>
        /// Creates a response to a command. If the result is larger than can be retured as a single message, 
        /// will upload as a text file.
        /// </summary>
        /// <param name="response">the string of the message to respond with.</param>
        public static async Task Respond(this IMessage message, string response) {
            await message.Channel.Respond(response);
        }

        static readonly Random Random = new Random();

        public static T SelectRandom<T>(this IEnumerable<T> t) {
            var array = t.ToArray();
            return array[Random.Next(array.Length)];
        }

        public static async Task Respond(this IMessageChannel channel, string response) {
            const string fileName = "results.txt";
            if (response.Length > DiscordConfig.MaxMessageSize)
                await channel.SendFileAsync(fileName, response);
            else {
                if(response.Length > 0)
                    await channel.SendMessageAsync(response);
            }
        }

        public static async Task Success(this IMessage message,
                                         string followup = null) {
            await message.Channel.Success(followup);
        } 

        public static async Task Success(this IMessageChannel channel, string followup) {
            await channel.Respond(Utility.Success(followup));
        }

        public static async Task SendFileRetry(this IMessageChannel user,
                                                string path,
                                                string text = null) {
            await Utility.FileIO(async () => {
                using (var file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                    await user.SendFileAsync(file, Path.GetFileName(path), text);
                }
            });
        }

        public static async Task SendFileRetry(this IMessage message, string path, string text = null) {
            await message.Channel.SendFileRetry(path, text);
        }

        //public static async Task SendFileRetry(this IGuildUser user,
        //                                       string path) {
        //    return user.
        //}

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

        //public static async Task SendMemoryFile(this IUser user,
        //                                  string name,
        //                                  string value) {
        //    using (var stream = new MemoryStream()) {
        //        var writer = new StreamWriter(stream);
        //        writer.Write(value);
        //        writer.Flush();
        //        stream.Position = 0;
        //        await user.SendFile(name, stream);
        //    }
        //}

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
