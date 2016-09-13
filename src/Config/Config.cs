using System;
using System.Collections.Generic;
using System.IO;
using Discord;
using Newtonsoft.Json;

namespace DrumBot {

    public class Config {

        public const string ConfigFilePath = "config.json";

        static readonly Dictionary<ulong, GuildConfig> _serversConfigs;

        public static IEnumerable<GuildConfig> ServerConfigs => _serversConfigs.Values;

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

        // The login token used by the bot to access Discord 
        [JsonProperty]
        public static string Token { get; set; }

        // The name of the bot.
        [JsonProperty]
        public static string BotName { get; set; }

        // The owner of the bot's ID.
        [JsonProperty]
        public static ulong Owner { get; set; }

        // The owner of the bot's ID.
        [JsonProperty]
        public static ulong TestServer { get; set; }

        [JsonProperty]
        public static string Version { get; set; }

        // The subdirectory name where the logs for each channel is logged.
        [JsonProperty]
        public static string LogDirectory { get; set; } = "logs";

        // The subdirectory where the configs for each guild is stored.
        [JsonProperty]
        public static string ConfigDirectory { get; set; } = "config";

        // The subdirectory where the feed is stored.
        [JsonProperty]
        public static string FeedDirectory { get; set; } = "feeds";

        [JsonProperty]
        public static string AvatarDirectory { get; set; } = "avatars";

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
