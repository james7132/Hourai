using System;
using System.Collections.Generic;
using System.IO;
using Discord;
using Newtonsoft.Json;

namespace DrumBot {
    [Serializable]
    public class Config {

        public const string ConfigFilePath = "config.json";

        static readonly Dictionary<ulong, GuildConfig> _serversConfigs;

        public IEnumerable<GuildConfig> ServerConfigs => _serversConfigs.Values;

        static Config() {
            _serversConfigs = new Dictionary<ulong, GuildConfig>();
        }

        public static void Load() {
            string fullPath = Path.Combine(Bot.ExecutionDirectory,
                ConfigFilePath);
            Log.Info($"Loading DrumBot config from {fullPath}...");
            JsonConvert.DeserializeObject<Config>(File.ReadAllText(ConfigFilePath));
            Log.Info($"Setting log directory to: { LogDirectory }");
            Log.Info($"Setting config directory to: { ConfigDirectory }");
            Log.Info("Config loaded.");
        }

        public static GuildConfig GetGuildConfig(IGuild guild) {
            if(!_serversConfigs.ContainsKey(guild.Id))
                _serversConfigs[guild.Id] = new GuildConfig(guild);
            return _serversConfigs[guild.Id];
        }

        public static GuildConfig GetGuildConfig(IChannel channel) {
            var guildChannel = Check.NotNull(channel) as IGuildChannel;
            if (guildChannel == null)
                return null;
            return GetGuildConfig(guildChannel.Guild);
        }

        public static ChannelConfig GetChannelConfig(IChannel channel) {
            return GetGuildConfig(channel)?.GetChannelConfig(channel);
        }

        /// <summary>
        /// The login token used by the bot to access Discord 
        /// </summary>
        [JsonProperty]
        public static string Token { get; set; }

        /// <summary>
        /// The name of the bot.
        /// </summary>
        [JsonProperty]
        public static string BotName { get; set; }

        /// <summary>
        /// The owner of the bot's ID.
        /// </summary>
        [JsonProperty]
        public static ulong Owner { get; set; }

        [JsonProperty]
        public static string Version { get; set; }

        /// <summary>
        /// The subdirectory name where the logs for each channel is logged.
        /// </summary>
        [JsonProperty]
        public static string LogDirectory { get; set; } = "logs";

        /// <summary>
        /// The subdirectory where the configs for each guild is stored.
        /// </summary>
        [JsonProperty]
        public static string ConfigDirectory { get; set; } = "config";

        [JsonProperty]
        public static string AvatarDirectory { get; set; } = "avatars";

        /// <summary>
        /// The command prefix that triggers commands specified by the bot
        /// </summary>
        [JsonProperty]
        public static char CommandPrefix { get; set; } = '~';

        /// <summary>
        /// What is responded when a command succeeds
        /// </summary>
        [JsonProperty]
        public static string SuccessResponse { get; set; } = ":thumbsup:";

        /// <summary>
        /// Maximum number of messages to remove with the prune command.
        /// </summary>
        [JsonProperty]
        public static int PruneLimit { get; set; } = 100;
    }
}
