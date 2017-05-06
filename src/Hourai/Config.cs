using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Hourai {

public class RedditConfig {
  public string Username { get; set; }
  public string Password { get; set; }
  public string ClientID { get; set; }
  public string ClientSecret { get; set; }
  public string RedirectUri { get; set; }
}

public class DiscordBotConfig  {
  // The login token used by the bot to access Discord
  public string Token { get; set; }
  // The default command prefix that triggers commands specified by the bot
  // This is override-able on a per-guild basis via "config prefix <x>"
  public char CommandPrefix { get; set; } = '~';
  // What is responded when a command succeeds
  public string SuccessResponse { get; set; } = ":thumbsup:";
  public List<string> ErrorBlacklist { get; set; } = new List<string>();
}

public class StorageConfig {
  // The subdirectory name where the logs for each channel is logged.
  public string LogDirectory { get; set; } = "logs";
  public string DbFilename { get; set; } = "./bot.db";
  public string ImageStoragePath { get; set; } = "images";
}

}
