using System;
using System.Collections.Generic;
using System.IO;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;

namespace Hourai {

public class Config {

  public const string BaseDataPath = "/var/bot/hourai";

#if DEBUG
  public const string ConfigFilePath = BaseDataPath + "/test_config.json";
#else
  public const string ConfigFilePath = BaseDataPath + "/config.json";
#endif


  public static bool IsLoaded { get; private set; }

  public static void Load() {
    if (IsLoaded)
      return;
    Log.Info($"Loading config from {ConfigFilePath}...");
    JsonConvert.DeserializeObject<Config>(File.ReadAllText(ConfigFilePath));
    Log.Info($"Setting log directory to: { LogDirectory }");
    Log.Info($"Setting database to: { DbFilename }");
    Log.Info("Config loaded.");
    IsLoaded = true;
  }

  // The login token used by the bot to access Discord
  [JsonProperty]
  public static string Token { get; set; }

  [JsonProperty]
  public static string Version { get; set; }

  // The subdirectory name where the logs for each channel is logged.
  [JsonProperty]
  public static string LogDirectory { get; set; } = "logs";

  [JsonProperty]
  public static string DbFilename { get; set; } = "./bot.db";

  [JsonProperty]
  public static string ImageStoragePath = BaseDataPath + "/images";

  [JsonProperty]
  public static DiscordSocketConfig DiscordConfig { get; set; }

  // The default command prefix that triggers commands specified by the bot
  // This is override-able on a per-guild basis via "config prefix <x>"
  [JsonProperty]
  public static char CommandPrefix { get; set; } = '~';

  // What is responded when a command succeeds
  [JsonProperty]
  public static string SuccessResponse { get; set; } = ":thumbsup:";

  [JsonProperty]
  public static string RedditUsername { get; set; }

  [JsonProperty]
  public static string RedditPassword { get; set; }

  [JsonProperty]
  public static string RedditClientID { get; set; }

  [JsonProperty]
  public static string RedditClientSecret { get; set; }

  [JsonProperty]
  public static string RedditRedirectUri { get; set; }

}
}
