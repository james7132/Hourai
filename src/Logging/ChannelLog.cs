using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;

namespace DrumBot {

    [InitializeOnLoad]
    public class ChannelLog {
        const string NewLineReplacement = "\\n";
        static readonly string LogDirectory;
        readonly string _channelDirectory;

        static ChannelLog() {
            LogDirectory = Path.Combine(DrumPath.ExecutionDirectory,
                Bot.Config.LogDirectory);
            Log.Info($"Chat Log Directory: { LogDirectory }");
        }

        public string GetPath(DateTime time) {
            return GetPath(time.ToString("yyyy-MM-dd"));
        }

        public string GetPath(string time) {
            return Path.Combine(_channelDirectory, time) + ".log";
        }

        public ChannelLog(Channel channel) {
            _channelDirectory = Path.Combine(LogDirectory,
                channel.Server.Id.ToString(),
                channel.Id.ToString());
            Log.Info($"Saving channel logs for { channel.Server.Name }'s #{ channel.Name} to { _channelDirectory }");
            if (!Directory.Exists(_channelDirectory))
                Directory.CreateDirectory(_channelDirectory);
        }

        string MessageToLog(string message) {
            return message.Replace("\n", NewLineReplacement);
        }

        string LogToMessage(string log) {
            return log.Replace(NewLineReplacement, "\n");
        }

        public async Task LogMessage(MessageEventArgs args) {
            try {
                var timestamp = args.Message.Timestamp;
                using (StreamWriter writer = File.AppendText(GetPath(timestamp)))
                    await writer.WriteLineAsync(MessageToLog($"{timestamp.DrumDateString()} - { args.Message }"));
            } catch(IOException ioException) {
                Log.Error(ioException);
            }
        }

        public async Task<string> Search(string exactMatch) {
            var builder = new StringBuilder();
            string[] files = Directory.GetFiles(_channelDirectory);
            await Task.WhenAll(files.Select(file => SearchFile(file, exactMatch, builder)));
            return LogToMessage(builder.ToString());
        }

        async Task SearchFile(string path, string exactMatch, StringBuilder builder) {
            try {
                using (StreamReader reader = File.OpenText(path)) {
                    while(!reader.EndOfStream) {
                        string line = await reader.ReadLineAsync();
                        if (line != null && line.Contains(exactMatch) && !line.Contains("~search") && !line.Contains("Bot: "))
                            builder.AppendLine(line);
                    }
                }
            } catch(IOException exception) {
                Log.Error(exception);
            }
        }
    }
}
