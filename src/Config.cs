using System;
using System.Collections.Generic;
using System.IO;
using Discord;
using Newtonsoft.Json;

namespace Hourai {

public class Config {

  public const string ConfigFilePath = "config.json";

  public static void Load() {
    string fullPath = Path.Combine(Bot.GetExecutionDirectory(),
        ConfigFilePath);
    Log.Info($"Loading config from {fullPath}...");
    JsonConvert.DeserializeObject<Config>(File.ReadAllText(ConfigFilePath));
    Log.Info($"Setting log directory to: { LogDirectory }");
    Log.Info($"Setting database to: { DbFilename }");
    Log.Info("Config loaded.");
  }

  // The login token used by the bot to access Discord 
  [JsonProperty]
  public static string Token { get; set; }

  [JsonProperty]
  public static ulong TestServer { get; set; }

  [JsonProperty]
  public static string Version { get; set; }

  // The subdirectory name where the logs for each channel is logged.
  [JsonProperty]
  public static string LogDirectory { get; set; } = "logs";

  [JsonProperty]
  public static string DbFilename { get; set; } = "./bot.db";

  // The command prefix that triggers commands specified by the bot
  [JsonProperty]
  public static char CommandPrefix { get; set; } = '~';

  // What is responded when a command succeeds
  [JsonProperty]
  public static string SuccessResponse { get; set; } = ":thumbsup:";

  // Maximum number of messages to remove with the prune command.
  [JsonProperty]
  public static int PruneLimit { get; set; } = 100;

}
}
