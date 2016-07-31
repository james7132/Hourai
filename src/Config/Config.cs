using System;
using System.Collections.Generic;
using System.IO;
using Discord;
using Newtonsoft.Json;

namespace DrumBot {
    [Serializable]
    [InitializeOnLoad(10000)]
    public class Config {

        public const string ConfigFilePath = "config.json";

        static Config() {
        }

        readonly Dictionary<ulong, ServerConfig> _serversConfigs;

        public Config() {
            _serversConfigs = new Dictionary<ulong, ServerConfig>();
        }

        public static Config Load() {
            string fullPath = Path.Combine(DrumPath.ExecutionDirectory,
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

        public string BotName { get; set; }
        public string Token { get; set; }
        public string LogDirectory { get; set; } = "logs";
        public string ConfigDirectory { get; set; } = "config";
        public char CommandPrefix { get; set; } = '~';
    }
}
