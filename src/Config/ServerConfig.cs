using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace DrumBot {

    public enum ServerType {
        TEST,
        PROD 
    }

    public interface IConfig {
        event Action OnEdit;
    }

    public class ChannelConfig : IConfig {
        [JsonProperty]
        bool isIgnored = false;

        [JsonProperty]
        ulong? group;
        public event Action OnEdit;

        public ChannelConfig(ulong id) { this.Id = id; }

        [JsonIgnore]
        public bool IsIgnored {
            get { return isIgnored; }
            set {
                bool changed = isIgnored == value;
                isIgnored = value; 
                if(changed)
                    OnEdit?.Invoke();
            }
        }

        [JsonIgnore]
        public ulong? Group {
            get { return group; }
            set {
                bool changed = group == value;
                group = value;
                if(changed)
                    OnEdit?.Invoke();
            }
        }

        public void CreateGroup() {
            bool changed = InGroup && group != Id;
            group = Id; 
            if(changed)
                OnEdit?.Invoke();
        }

        public void Ignore() {
            bool changed = !isIgnored;
            isIgnored = true;
            if(changed)
                OnEdit?.Invoke();
        }

        public void Unignore() {
            bool changed = isIgnored;
            isIgnored = false;
            if(changed)
                OnEdit?.Invoke();
        }

        [JsonIgnore]
        public bool InGroup => group != null;
        [JsonIgnore]
        public bool IsMainChannel => InGroup && group == Id;
        [JsonIgnore]
        public bool IsSubchannel => InGroup && group != Id;

        [JsonIgnore]
        public ulong Id { get; set; }
    }

    public class UserConfig : IConfig {
        public event Action OnEdit;

        [JsonIgnore]
        public ulong Id { get; set; }
        [JsonProperty]
        HashSet<ulong> bannedRoles;

        public UserConfig(ulong id) {
            Id = id;
            bannedRoles = new HashSet<ulong>();
        }

        public async Task RemoveBannedRoles(IGuildUser user) {
            if (bannedRoles.Count <= 0)
                return;
            await user.RemoveRolesAsync(user.Roles.Where(r => bannedRoles.Contains(r.Id)));
        }

        public bool IsRoleBanned(IRole role) {
            return bannedRoles.Contains(role.Id);
        }

        public bool BanRole(IRole role) {
            bool success = bannedRoles.Add(Check.NotNull(role).Id);
            if(success)
                OnEdit?.Invoke();
            return success;
        }

        public bool UnbanRole(IRole role) {
            bool success = bannedRoles.Remove(Check.NotNull(role).Id);
            if(success)
                OnEdit?.Invoke();
            return success;
        }
    }

    public class CustomCommand : IConfig {
        public event Action OnEdit;

        [JsonProperty]
        string name;

        [JsonProperty]
        string response;

        [JsonIgnore]
        public string Name {
            get { return name; }
            set {
                bool changed = name != value;
                name = value;
                if(changed)
                    OnEdit?.Invoke();
            }
        }

        [JsonIgnore]
        public string Response {
            get { return response; }
            set {
                bool changed = response != value;
                response = value;
                if(changed)
                    OnEdit?.Invoke();
            }
        }

        public CustomCommand(string name) { this.name = name; }

        //public void CreateCommand(CommandGroupBuilder builder, Server server) {
        //    builder.CreateCommand(name)
        //        .Parameter("Input", ParameterType.Unparsed)
        //        .AddCheck((cm, u, ch) => ch.Server == server)
        //        .AddCheck((cm, u, ch) => Config.GetGuildConfig(ch.Server)
        //            .CommandService.FirstOrDefault(c => c.Name.Equals(cm.Text, StringComparison.OrdinalIgnoreCase)) != null)
        //        .Do(async e => await e.Respond(response
        //                .Replace("$input", e.GetArg("Input"))
        //                .Replace("$user", e.User.Mention)
        //                .Replace("$channel", e.Channel.Mention)));
        //}
    }

    public class ServerConfig {

        [JsonIgnore]
        public ulong ID { get; set; }
        public ServerType Type { get; set; } = ServerType.PROD;

        [JsonProperty]
        Dictionary<ulong, ChannelConfig> ChannelConfigs { get; set; }

        [JsonProperty]
        Dictionary<ulong, UserConfig> UserConfigs { get; set; }

        [JsonProperty]
        List<CustomCommand> CustomCommands { get; set; }

        [JsonProperty]
        Dictionary<string, ulong> MinimumRoles { get; set; }

        [JsonIgnore]
        public IGuild Server { get; }
        public HashSet<string> Modules { get; set; }
        public List<TempUserAction> TempActions { get; set; }

        [JsonIgnore] 
        public static string ConfigDirectory => Path.Combine(Bot.ExecutionDirectory, Config.ConfigDirectory);

        [JsonIgnore]
        public string SaveLocation => Path.Combine(ConfigDirectory, ID + ".config.json");

        public ServerConfig(IGuild server) {
            Server = server;
            ID = server.Id;
            Modules = new HashSet<string>();
            ChannelConfigs = new Dictionary<ulong, ChannelConfig>();
            UserConfigs = new Dictionary<ulong, UserConfig>();
            CustomCommands = new List<CustomCommand>();
            TempActions = new List<TempUserAction>();
            MinimumRoles = new Dictionary<string, ulong>();
            Log.Info($"Loading server configuration for { server.ToIDString() } from { SaveLocation }");
            if (File.Exists(SaveLocation))
                Load().Wait();
        }

        public ChannelConfig GetChannelConfig(IChannel channel) {
            var id = Check.NotNull(channel).Id;
            if (!ChannelConfigs.ContainsKey(id)) {
                var config = new ChannelConfig(channel.Id);
                config.OnEdit += Save;
                ChannelConfigs[id] = config;
            }
            return ChannelConfigs[id];
        }

        public UserConfig GetUserConfig(IGuildUser user) {
            var id = Check.NotNull(user).Id;
            if (!UserConfigs.ContainsKey(id)) {
                var config = new UserConfig(id);
                config.OnEdit += Save;
                UserConfigs[id] = config;
            }
            return UserConfigs[id];
        }

        public void SetMinimumRole(string name, IRole minimumRole) {
            MinimumRoles[name] = minimumRole.Id;
            Save();
        }

        public ulong? GetMinimumRole(string name) {
            if (!MinimumRoles.ContainsKey(name))
                return null;
            return MinimumRoles[name];
        }

        public CustomCommand GetCustomCommand(string name) {
            return CustomCommands.Find(c => c.Name == name);
        }

        public CustomCommand AddCustomCommand(string name) {
            var command = new CustomCommand(name);
            CustomCommands.Add(command);
            Save();
            return command;
        }

        public void RemoveCustomCommand(string name) {
            CustomCommands.RemoveAll(c => c.Name == name);
            Save();
        }

        [OnDeserialized]
        void OnDeserialize(StreamingContext context) {
            foreach (var config in ChannelConfigs) {
                config.Value.Id = config.Key;
                config.Value.OnEdit += Save;
            }
            foreach (var config in UserConfigs) {
                config.Value.Id = config.Key;
                config.Value.OnEdit += Save;
            }
        }

        public async Task Cleanup(DiscordSocketClient client) {
            var server = await client.GetGuildAsync(ID);
            if (server == null)
                return;
            //foreach (var group in Groups.ToArray()) {
            //    if (server.GetChannel(group.Key) == null)
            //        Groups.RemoveCustomCommand(group.Key);
            //    foreach (ulong channel in group.Value.ToArray())
            //        group.Value.RemoveCustomCommand(channel);
            //}
            Save();
        }

        public async Task Update(DiscordSocketClient client) {
            var server = await client.GetGuildAsync(ID); 
            if (server == null)
                return;
            var changed = false;
            foreach (TempUserAction tempUserAction in TempActions.ToArray()) {
                if (tempUserAction.Expiration > DateTime.Now)
                    continue;
                await tempUserAction.Undo(server);
                var defaultChannel = await server.GetChannelAsync(server.DefaultChannelId) as IMessageChannel;
                await defaultChannel.SendMessageAsync("Temp Action Undone.");
                TempActions.Remove(tempUserAction);
                changed = true;
            }
            if (changed)
                Save();
        }

        [JsonIgnore]
        public IEnumerable<CustomCommand> Commands => CustomCommands;

        public void AddModule(string name) {
            if (Modules.Add(name))
                Save();
        }

        public bool IsModuleEnabled(string name) {
            return Modules.Contains(name);
        }

        public void RemoveModule(string name) {
            if (Modules.Remove(name))
               Save();
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

        public async void Save() {
            if (!Directory.Exists(ConfigDirectory))
                Directory.CreateDirectory(ConfigDirectory);
            Log.Info("SAVED");
            await Utility.FileIO(async delegate {
                using(var file = File.Open(SaveLocation, FileMode.Create, FileAccess.Write))
                using(var writer = new StreamWriter(file))
                    await writer.WriteAsync(JsonConvert.SerializeObject(this, Formatting.Indented, new StringEnumConverter()));
            });
        }

        public async Task Load() {
            string obj = string.Empty;
            await Utility.FileIO(async delegate {
                using (var file = File.OpenText(SaveLocation))
                    obj = await file.ReadToEndAsync();
            });
            JsonConvert.PopulateObject(obj, this);
        }

        public override string ToString() {
            var builder = new StringBuilder();
            builder.AppendLine($"ID: {ID}");
            builder.AppendLine($"Type: {Type}");
            builder.AppendLine("Roles:");
            foreach (IRole role in Server.Roles) {
                builder.AppendLine($"   {role.Name}: {role.Position}, {role.Color}, {role.Id}");
            }
            return builder.ToString().Wrap("```");
        }
    }
}
