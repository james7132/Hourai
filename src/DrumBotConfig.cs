using System;
using System.IO;
using Newtonsoft.Json;

namespace DrumBot {
    [Serializable]
    [InitializeOnLoad(10000)]
    public class DrumBotConfig {

        public const string ConfigFilePath = "config.json";

        public static DrumBotConfig Instance { get; }

        static DrumBotConfig() {
            string fullPath = Path.Combine(DrumPath.ExecutionDirectory,
                ConfigFilePath);
            Log.Info($"Loading DrumBot config from {fullPath}...");
            Instance = JsonConvert.DeserializeObject<DrumBotConfig>(
                            File.ReadAllText(ConfigFilePath));
            Log.Info($"Setting log directory to: { Instance.LogDirectory }");
            Log.Info($"Setting config directory to: { Instance.ConfigDirectory }");
            Log.Info("Config loaded.");
        }

        public string Token { get; set; }
        public string LogDirectory { get; set; } = "logs";
        public string ConfigDirectory { get; set; } = "config";
        public char CommandPrefix { get; set; } = '~';
    }
}
