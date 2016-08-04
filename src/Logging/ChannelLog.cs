using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;

namespace DrumBot {

    [InitializeOnLoad]
    public class ChannelLog {

        /// <summary>
        /// A replacement for all new lines to keep all messages on one line while logging.
        /// </summary>
        const string NewLineReplacement = "\\n";

        /// <summary>
        /// The absolute path to the directory where all of the logs are stored.
        /// </summary>
        static readonly string LogDirectory;

        /// <summary>
        /// The directory where all of the logs for specifically the channel described here is stored.
        /// </summary>
        readonly string _channelDirectory;

        static ChannelLog() {
            LogDirectory = Path.Combine(Bot.ExecutionDirectory, Config.LogDirectory);
            Log.Info($"Chat Log Directory: { LogDirectory }");
        }

        /// <summary>
        /// Gets the path of the log file for this channel on a certain day.
        /// </summary>
        /// <param name="time">the day specified</param>
        /// <returns>the path to the log file</returns>
        public string GetPath(DateTime time) {
            return GetPath(time.ToString("yyyy-MM-dd"));
        }

        // Same as above, except with direct access.
        public string GetPath(string time) {
            return Path.Combine(_channelDirectory, time) + ".log";
        }

        public ChannelLog(Channel channel) {
            _channelDirectory = Path.Combine(LogDirectory,
                channel.Server.Id.ToString(),
                channel.Id.ToString());
            Log.Info($"Saving channel logs for { channel.Server.Name }'s #{ channel.Name} to { _channelDirectory }");
        }

        string MessageToLog(string message) {
            return message.Replace("\n", NewLineReplacement);
        }

        string LogToMessage(string log) {
            return log.Replace(NewLineReplacement, "\n");
        }

        /// <summary>
        /// Logs a message.
        /// </summary>
        /// <param name="message">the message to log</param>
        public async Task LogMessage(Message message) {
            if(message == null)
                throw new ArgumentNullException();
            if (!Directory.Exists(_channelDirectory))
                Directory.CreateDirectory(_channelDirectory);
            var timestamp = message.Timestamp;
            await Utility.FileIO(async delegate {
                using (StreamWriter writer = File.AppendText(GetPath(timestamp)))
                    await writer.WriteLineAsync(MessageToLog($"{Utility.DateString(timestamp)} - { message.ToProcessedString() }"));
            });
        }

        /// <summary>
        /// Searches all logs for instances of a certain exact match.
        /// </summary>
        /// <param name="exactMatch">the string to look for</param>
        /// <returns>all matches in a string</returns>
        public async Task<string> Search(string exactMatch) {
            if (!Directory.Exists(_channelDirectory))
                return string.Empty;
            string[] files = Directory.GetFiles(_channelDirectory);
            string[] results = await Task.WhenAll(files.Select(file => SearchFile(file, exactMatch)));
            return LogToMessage(string.Join("\n", results.Where(s => !string.IsNullOrEmpty(s))));
        }

        /// <summary>
        /// Searches a single file for results.
        /// </summary>
        /// <param name="path">the path to the file</param>
        /// <param name="exactMatch">the exact match to search for</param>
        async Task<string> SearchFile(string path, string exactMatch) {
            var builder = new StringBuilder();
            Func<Task> read = async delegate {
                using (StreamReader reader = File.OpenText(path)) {
                    while(!reader.EndOfStream) {
                        string line = await reader.ReadLineAsync();
                        if (line != null && line.Contains(exactMatch) && !line.Contains("~search") && !line.Contains("Bot: "))
                            builder.AppendLine(line);
                    }
                }
            };
            Action retry = delegate { builder.Clear(); };
            await Utility.FileIO(read, retry);
            return builder.ToString();
        }
    }
}
