using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace DrumBot {

    public enum ServerType {
        TEST,
        PROD 
    }

    public class ServerConfig {

        [JsonIgnore]
        public ulong ID { get; set; }
        public ServerType Type { get; set; } = ServerType.PROD;
        [JsonIgnore]
        public Server Server { get; }
        public HashSet<ulong> IgnoredChannels { get; set; }

        [JsonIgnore] 
        public static string ConfigDirectory => Path.Combine(DrumPath.ExecutionDirectory,
            Bot.Config.ConfigDirectory);

        [JsonIgnore]
        public string SaveLocation => Path.Combine(ConfigDirectory, ID + ".config.json");

        public ServerConfig(Server server) {
            Server = server;
            ID = server.Id;
            if (IgnoredChannels == null)
                IgnoredChannels = new HashSet<ulong>();
            Log.Info($"Loading server configuration for { server.ToIDString() } from { SaveLocation }");
            if (!File.Exists(SaveLocation))
                Save().Wait();
            else
                Load().Wait();
        }

        public bool IsIgnored(Channel channel) {
            return IgnoredChannels.Contains(channel.Id);
        }

        public async void AddIgnoredChannels(params ulong[] channels) {
            IgnoredChannels.UnionWith(channels);
            Log.Info(IgnoredChannels.Count);
            await Save();
        }

        public async void RemoveIgnoredChannels(params ulong[] channels) {
            IgnoredChannels.ExceptWith(channels);
            Log.Info(IgnoredChannels.Count);
            await Save();
        }
        
        [JsonIgnore]
        public bool AllowCommands {
            get {
#if DEBUG
                return Type == ServerType.TEST;
#else
                return Type == ServerType.PROD;
#endif
            }
        }

        public async Task Save() {
            if (!Directory.Exists(ConfigDirectory))
                Directory.CreateDirectory(ConfigDirectory);
            using(var file = File.Open(SaveLocation, FileMode.OpenOrCreate, FileAccess.Write))
            using(var writer = new StreamWriter(file))
                await writer.WriteAsync(JsonConvert.SerializeObject(this, Formatting.Indented, new StringEnumConverter()));
        }

        public async Task Load() {
            string obj;
            using (var file = File.OpenText(SaveLocation))
                obj = await file.ReadToEndAsync();
            JsonConvert.PopulateObject(obj, this);
        }

        public override string ToString() {
            var builder = new StringBuilder();
            builder.AppendLine($"ID: {ID}");
            builder.AppendLine($"Type: {Type}");
            builder.AppendLine("Roles:");
            foreach (Role role in Server.Roles) {
                builder.AppendLine(
                    $"{role.Name}: {role.Position}, {role.Color}, {role.Id}");
            }
            return builder.ToString().Wrap("```");
        }
    }
}
