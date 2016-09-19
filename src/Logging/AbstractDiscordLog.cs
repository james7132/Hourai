using Discord;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Hourai {

public abstract class AbstractDiscordLog {

  protected const string DateFormat = "yyyy-MM-dd";
  protected const string FileType = ".log";

  /// <summary>
  /// The absolute path to the directory where all of the logs are stored.
  /// </summary>
  public static readonly string LogDirectory;

  static AbstractDiscordLog() {
    LogDirectory = Path.Combine(Bot.ExecutionDirectory, Config.LogDirectory);
    Log.Info($"Chat Log Directory: { LogDirectory }");
  }

  public virtual string SaveDirectory { get; protected set; } 
  public string GuildDirectory { get; protected set; }

  public static string GetGuildDirectory(IGuild guild) {
    return Path.Combine(LogDirectory, Check.NotNull(guild).Id.ToString());
  }

  public static string GetChannelDirectory(IGuildChannel channel) {
    return Path.Combine(GetGuildDirectory(channel.Guild), channel.Id.ToString());
  }

  public abstract string GetPath(DateTimeOffset time);

  public Task LogEvent(string message, DateTimeOffset? time = null) {
    DateTimeOffset timestamp = time ?? DateTimeOffset.Now;
    var path = GetPath(timestamp);
    return Utility.FileIO(async delegate {
        using (StreamWriter writer = File.AppendText(path))
          await writer.WriteLineAsync($"{Utility.DateString(timestamp)} - {message}");
      });
  }

}

}
