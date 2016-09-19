using Discord;
using System;
using System.IO;

namespace Hourai {

public class GuildLog : AbstractDiscordLog {

  public IGuild Guild { get; }

  public GuildLog(IGuild guild) {
    Guild = guild;
    SaveDirectory = GetGuildDirectory(guild);
  }

  public override string GetPath(DateTimeOffset time) {
    return Path.Combine(SaveDirectory, $"server-{time.ToString("yyyy-MM")}.log");
  }

}

}
