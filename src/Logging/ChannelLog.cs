using System;
using System.Net;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Net;

namespace Hourai {

public class ChannelLog : AbstractDiscordLog {

  /// <summary>
  /// A replacement for all new lines to keep all messages on one line while logging.
  /// </summary>
  const string NewLineReplacement = "\\n";

  public ITextChannel Channel { get; }

  /// <summary>
  /// The directory where all of the logs for specifically the channel described here is stored.
  /// </summary>
  public string ChannelDirectory { get; }

  /// <summary>
  /// Gets the path of the log file for this channel on a certain day.
  /// </summary>
  /// <param name="time">the day specified</param>
  /// <returns>the path to the log file</returns>
  public override string GetPath(DateTimeOffset time) {
    return GetPath(time.ToString(DateFormat));
  }

  // Same as above, except with direct access.
  public string GetPath(string time) {
    return Path.Combine(ChannelDirectory, time) + FileType;
  }

  public bool Initialized => Directory.Exists(ChannelDirectory);

  public async Task Initialize() {
    if (!Initialized) {
      Directory.CreateDirectory(ChannelDirectory);
      Log.Info($"Logs for { Channel.Name } do not exist. Downloading the most recent messages.");
      LogChannelRecent(Channel);
      await Task.Delay(400);
    }
  }

  public ChannelLog(ITextChannel channel) {
    Channel = channel;
    GuildDirectory = GetGuildDirectory(channel.Guild);
    ChannelDirectory = GetChannelDirectory(channel);
    SaveDirectory = ChannelDirectory;
  }

  public async Task DeletedChannel(ITextChannel channel) {
    if (!Directory.Exists(ChannelDirectory))
      return;
    var channelConfig = await Bot.Database.GetChannel(channel);
    if(channelConfig.SearchIgnored) {
      Log.Info("Ignored channel deleted. Deleting logs...");
      await Utility.FileIO(() => Directory.Delete(ChannelDirectory, true));
    } else {
      var targetDirectory = Path.Combine(GuildDirectory,
          $"Deleted Channel {channel.Name} ({channel.Id})");
      Log.Info("Channel deleted. Moving logs...");
      await Utility.FileIO(() => Directory.Move(ChannelDirectory, targetDirectory));
    }
  }

  async void LogChannelRecent(ITextChannel channel) {
    try {
      var messages = await channel.GetMessagesAsync();
      foreach (var message in messages.OrderByDescending(m => m.Timestamp))
        await LogMessage(message);
    } catch(HttpException httpException) {
      if(httpException.StatusCode != HttpStatusCode.Forbidden)
        Log.Error(httpException);
    }
  }

  static string MessageToLog(string message) {
    return message.Replace("\n", NewLineReplacement);
  }

  static string LogToMessage(string log) {
    return log.Replace(NewLineReplacement, "\n");
  }

  /// <summary>
  /// Logs a message.
  /// </summary>
  /// <param name="message">the message to log</param>
  public Task LogMessage(IMessage message) {
    Check.NotNull(message);
    return LogEvent(MessageToLog(message.ToProcessedString()), message.Timestamp);
  }

  /// <summary>
  /// Searches all logs for instances of a certain exact match.
  /// </summary>
  /// <returns>all matches in a string</returns>
  public Task<string> Search(Func<string, bool> pred) {
    return SearchDirectory(pred, ChannelDirectory);
  }

  public Task<string> SearchAll(Func<string, bool> pred) {
    return SearchDirectory(pred, GuildDirectory);
  }

  public async Task<string> SearchDirectory(Func<string, bool> pred, string directory) {
    if (!Directory.Exists(directory))
      return string.Empty;
    if(Channel == null)
      throw new InvalidOperationException("A channel must be defined to search the target directory");
    var guild = Channel.Guild;
    var guildConfig = await Bot.Database.GetGuild(guild);
    var res =
        from file in Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories).AsParallel()
        from line in File.ReadLines(file)
        where pred(line)
        group line by Directory.GetParent(file).Name into g
        orderby g.Key
        select g;
    var builder = new StringBuilder();
    foreach (var re in res) {
      if (!re.Any())
        continue;
      var name = re.Key;
      Log.Info(name);
      ulong id;
      if(ulong.TryParse(name, out id)) {
        var channel = await guild.GetChannelAsync(id);
        if (channel != null) {
          var config = await Bot.Database.GetChannel(channel);
          if(channel != Channel && config.SearchIgnored)
            continue;
          name = channel.Name;
        }
      }
      builder.AppendLine($"Match results in { name }: ".Bold());
      builder.AppendLine(re.OrderBy(s => s).Join("\n"));
    }
    return LogToMessage(builder.ToString());
  }

  /// <summary>
  /// Searches a single file for results.
  /// </summary>
  /// <param name="path">the path to the file</param>
  static async Task<string> SearchFile(string path, Func<string, bool> pred) {
    var builder = new StringBuilder();
    Func<Task> read = async delegate {
      using (StreamReader reader = File.OpenText(path)) {
        while(!reader.EndOfStream) {
          string line = await reader.ReadLineAsync();
          if (line != null && pred(line))
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
