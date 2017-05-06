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
  public string LogDirectory { get; }

  protected AbstractDiscordLog(string logDirectory) {
    LogDirectory = logDirectory;
  }

  public virtual string SaveDirectory { get; protected set; }
  public string GuildDirectory { get; protected set; }

  protected string GetGuildDirectory(IGuild guild) {
    return Path.Combine(LogDirectory, Check.NotNull(guild).Id.ToString());
  }

  public abstract string GetPath(DateTimeOffset time);

  public Task LogEvent(string message, DateTimeOffset? time = null) {
    DateTimeOffset timestamp = time ?? DateTimeOffset.Now;
    var path = GetPath(timestamp);
    var dir = Path.GetDirectoryName(path);
    if (!Directory.Exists(dir))
      Directory.CreateDirectory(dir);
    return Utility.FileIO(async delegate {
        using (StreamWriter writer = File.AppendText(path))
          await writer.WriteLineAsync($"{Utility.DateString(timestamp)} - {message}");
      });
  }

}

}
