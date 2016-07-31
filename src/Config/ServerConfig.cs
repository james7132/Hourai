using System.IO;
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
        public static string ConfigDirectory => Path.Combine(DrumPath.ExecutionDirectory,
            DrumBot.Config.ConfigDirectory);

        [JsonIgnore]
        public string SaveLocation => Path.Combine(ConfigDirectory, ID + ".config.json");

        public ServerConfig(Server server) {
            ID = server.Id;
            Log.Info($"Loading server configuration for { server.ToIDString() } from { SaveLocation }");
            if (!File.Exists(SaveLocation))
                Save().Wait();
            else
                Load().Wait();
        }

        [JsonIgnore]
        public bool AllowCommands {
            get {
#if DEBUG
                return Type != ServerType.PROD;
#else
                return true;
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
            return $"``ID: {ID}\nType: {Type}``";
        }
    }
}
