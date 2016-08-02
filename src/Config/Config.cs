using System;
using System.Collections.Generic;
using System.IO;
using Discord;
using Newtonsoft.Json;

namespace DrumBot {
    [Serializable]
    public class Config {

        public const string ConfigFilePath = "config.json";

        readonly Dictionary<ulong, ServerConfig> _serversConfigs;

        public Config() {
            _serversConfigs = new Dictionary<ulong, ServerConfig>();
        }

        public static Config Load() {
            string fullPath = Path.Combine(Bot.ExecutionDirectory,
                ConfigFilePath);
            Log.Info($"Loading DrumBot config from {fullPath}...");
            var config = JsonConvert.DeserializeObject<Config>(
                            File.ReadAllText(ConfigFilePath));
            Log.Info($"Setting log directory to: { config.LogDirectory }");
            Log.Info($"Setting config directory to: { config.ConfigDirectory }");
            Log.Info("Config loaded.");
            return config;
        }

        public ServerConfig GetServerConfig(Server server) {
            if(!_serversConfigs.ContainsKey(server.Id))
                _serversConfigs[server.Id] = new ServerConfig(server);
            return _serversConfigs[server.Id];
        }

        /// <summary>
        /// The login token used by the bot to access Discord 
        /// </summary>
        public string Token { get; set; }

        /// <summary>
        /// The name of the bot.
        /// </summary>
        public string BotName { get; set; }

        /// <summary>
        /// The owner of the bot's ID.
        /// </summary>
        public ulong Owner { get; set; }

        /// <summary>
        /// The subdirectory name where the logs for each channel is logged.
        /// </summary>
        public string LogDirectory { get; set; } = "logs";

        /// <summary>
        /// The subdirectory where the configs for each server is stored.
        /// </summary>
        public string ConfigDirectory { get; set; } = "config";

        /// <summary>
        /// The command prefix that triggers commands specified by the bot
        /// </summary>
        public char CommandPrefix { get; set; } = '~';

        /// <summary>
        /// What is responded when a command succeeds
        /// </summary>
        public string SuccessResponse { get; set; } = ":thumbsup:";

        /// <summary>
        /// Maximum number of messages to remove with the prune command.
        /// </summary>
        public int PruneLimit { get; set; } = 100;
    }
}
