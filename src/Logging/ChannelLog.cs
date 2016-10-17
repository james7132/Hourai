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

  public async Task DeletedChannel(ITextChannel channel, bool ignored = false) {
    if (!Directory.Exists(ChannelDirectory))
      return;
    if(ignored) {
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
      await channel.GetMessagesAsync()
        .SelectMany(m => m.ToAsyncEnumerable())
        .OrderByDescending(m => m.Timestamp)
        .ForEachAsync(async m => await LogMessage(m));
    } catch(HttpException httpException) {
      if(httpException.StatusCode != HttpStatusCode.Forbidden)
        Log.Error(httpException);
    }
  }

  //TODO(james7132): Move ths to a more general utility location.
  public static string MessageToLog(string message) {
    return message.Replace("\n", NewLineReplacement);
  }

  public static string LogToMessage(string log) {
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

}

}
