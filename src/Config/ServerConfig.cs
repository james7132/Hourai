using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using DrumBot.src;
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
        public HashSet<string> Modules { get; set; }
        public List<TempUserAction> TempActions { get; set; }
        public HashSet<ulong> IgnoredChannels { get; set; }
        public Dictionary<ulong, HashSet<ulong>> BannedRoles { get; set; }
        public Dictionary<ulong, HashSet<ulong>> Groups { get; set; }

        [JsonIgnore] 
        public static string ConfigDirectory => Path.Combine(Bot.ExecutionDirectory, Config.ConfigDirectory);

        [JsonIgnore]
        public string SaveLocation => Path.Combine(ConfigDirectory, ID + ".config.json");

        public ServerConfig(Server server) {
            Server = server;
            ID = server.Id;
            Modules = new HashSet<string>();
            TempActions = new List<TempUserAction>();
            IgnoredChannels = new HashSet<ulong>();
            BannedRoles = new Dictionary<ulong, HashSet<ulong>>();
            Groups = new Dictionary<ulong, HashSet<ulong>>();
            Log.Info($"Loading server configuration for { server.ToIDString() } from { SaveLocation }");
            if (File.Exists(SaveLocation))
                Load().Wait();
        }

        public bool IsRoleBanned(User user, Role role) {
            return BannedRoles.ContainsKey(user.Id) && BannedRoles[user.Id].Contains(role.Id);
        }

        public bool IsUserBannedFromRoles(User user) {
            return BannedRoles.ContainsKey(user.Id);
        }

        public async Task RemoveBannedRoles(User user) {
            if (!IsUserBannedFromRoles(user))
                return;
            var toRemove = new List<Role>();
            var bannedRoles = BannedRoles[user.Id];
            foreach (var role in user.Roles) {
                if(bannedRoles.Contains(role.Id))
                    toRemove.Add(role);
            }
            if(toRemove.Count > 0)
                await user.RemoveRoles(toRemove.ToArray());
        }

        public void BanUsersFromRole(Role role, params User[] users) {
            foreach (User user in users) {
                if (!BannedRoles.ContainsKey(user.Id))
                    BannedRoles[user.Id] = new HashSet<ulong>();
                BannedRoles[user.Id].Add(role.Id);
            }
        }

        public void UnbanUserFromRole(Role role, params User[] users) {
            foreach (User user in users) {
                if (!BannedRoles.ContainsKey(user.Id))
                    return;
                var roles = BannedRoles[user.Id];
                roles.Remove(role.Id);
                if (roles.Count <= 0)
                    BannedRoles.Remove(user.Id);
            }
        }

        public bool IsMainChannel(Channel channel) {
            return IsMainChannel(channel.Id);
        }

        public bool IsMainChannel(ulong channelId) {
            return Groups.ContainsKey(channelId);
        }

        public bool IsSubchannel(Channel channel) {
            return IsSubchannel(channel.Id);
        }

        public bool IsSubchannel(ulong channelId) {
            return Groups.Values.Any(g => g.Contains(channelId));
        }

        public bool InChannelGroup(Channel channel) {
            return InChannelGroup(channel.Id);
        }
        
        public bool InChannelGroup(ulong channelId) {
            return IsMainChannel(channelId) || IsSubchannel(channelId);
        }

        public async Task<bool> RemoveChannel(Channel channel) {
            return await RemoveChannel(channel.Id);
        }

        public async Task<bool> RemoveChannel(ulong channelID) {
            bool success = Groups.Values.Any(g => g.Remove(channelID));
            await Save();
            return success;
        }

        public async Task<bool> RemoveChannelGroup(Channel mainChannel) {
            bool success = Groups.Remove(mainChannel.Id);
            await Save();
            return success;
        }

        public IEnumerable<Channel> GetChannelGroup(Channel channel) {
            var channelGroup =
                Groups.FirstOrDefault(g => g.Value.Contains(channel.Id));
            if (channelGroup.Value == null)
                yield break;
            var server = channel.Server;
            var mainChannel = server.GetChannel(channelGroup.Key);
            yield return mainChannel;
            foreach (var groupChannelId in channelGroup.Value.ToArray())
                yield return server.GetChannel(groupChannelId);
        }

        public async Task Cleanup(DiscordClient client) {
            var server = client.GetServer(ID);
            if (server == null)
                return;
            foreach (var group in Groups.ToArray()) {
                if (server.GetChannel(group.Key) == null)
                    Groups.Remove(group.Key);
                foreach (ulong channel in group.Value.ToArray())
                    group.Value.Remove(channel);
            }
            await Save();
        }

        public async Task Update(DiscordClient client) {
            var server = client.GetServer(ID); 
            if (server == null)
                return;
            var changed = false;
            foreach (TempUserAction tempUserAction in TempActions.ToArray()) {
                if (tempUserAction.Expiration > DateTime.Now)
                    continue;
                await tempUserAction.Undo(server);
                await server.DefaultChannel.SendMessage("Temp Action Undone.");
                TempActions.Remove(tempUserAction);
                changed = true;
            }
            if (changed)
                await Save();
        }

        public Channel GetMainChannel(Channel channel) {
            ulong id = Groups.FirstOrDefault(g => g.Value.Contains(channel.Id)).Key;
            if(id == default(ulong))
                throw new InvalidOperationException($"A main channel for { channel.Mention } cannot be found.");
            Channel mainChannel = channel.Server.GetChannel(id);
            return mainChannel;
        }

        public async Task CreateChannelGroup(Channel mainChannel) {
            if(IsMainChannel(mainChannel))
                throw new InvalidOperationException($"{mainChannel.Mention} is already a main channel of a channel group. Cannot create new group.");
            if(IsSubchannel(mainChannel))
                throw new InvalidOperationException($"{mainChannel.Mention} is already a subchannel of { GetMainChannel(mainChannel).Mention }");
            Groups.Add(mainChannel.Id, new HashSet<ulong>());
            await Save();
        }

        public bool IsIgnored(Channel channel) {
            return IgnoredChannels.Contains(channel.Id);
        }

        public async Task AddIgnoredChannels(IEnumerable<ulong> channels) {
            IgnoredChannels.UnionWith(channels);
            await Save();
        }

        public Task AddIgnoredChannels(IEnumerable<Channel> channels) {
            return AddIgnoredChannels(channels.Select(ch => ch.Id));
        }

        public async Task RemoveIgnoredChannels(IEnumerable<ulong> channels) {
            IgnoredChannels.ExceptWith(channels);
            await Save();
        }

        public Task RemoveIgnoredChannels(IEnumerable<Channel> channels) {
            return RemoveIgnoredChannels(channels.Select(ch => ch.Id));
        }

        public async Task AddModule(string name) {
            if (Modules.Add(name))
                await Save();
        }

        public bool IsModuleEnabled(string name) {
            return Modules.Contains(name);
        }

        public async Task RemoveModule(string name) {
            if (Modules.Remove(name))
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
            foreach (Role role in Server.Roles) {
                builder.AppendLine(
                    $"   {role.Name}: {role.Position}, {role.Color}, {role.Id}");
            }
            return builder.ToString().Wrap("```");
        }
    }
}
