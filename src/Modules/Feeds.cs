using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace DrumBot {

    public abstract class JsonSaveable {

        protected abstract string DirectoryName { get; }
        protected abstract string FileName { get; }
        protected virtual IEnumerable<IEditable> Editables => Enumerable.Empty<IEditable>();

        [JsonIgnore]
        public string SaveDirectory => Path.Combine(Bot.ExecutionDirectory, DirectoryName);

        [JsonIgnore]
        public string SaveLocation => Path.Combine(Bot.ExecutionDirectory, DirectoryName, FileName + ".json");

        [OnDeserialized]
        void OnDeserialize(StreamingContext context) {
            foreach (var edits in Editables)
                Register(edits);
        }

        public void Register(IEditable editable) {
            Check.NotNull(editable).OnEdit += Save;
        }

        public void Unregister(IEditable editable) {
            Check.NotNull(editable).OnEdit -= Save;
        }

        public void LoadIfFileExists() {
            if (File.Exists(SaveLocation))
                Load().Wait();
        }

        public virtual async void Save() {
            if (!Directory.Exists(DirectoryName))
                Directory.CreateDirectory(DirectoryName);
            await Utility.FileIO(async delegate {
                using(var file = File.Open(SaveLocation, FileMode.Create, FileAccess.Write, FileShare.Write))
                using(var writer = new StreamWriter(file))
                    await writer.WriteAsync(JsonConvert.SerializeObject(this, Formatting.Indented, new StringEnumConverter()));
            });
            Log.Info($"Saved file to {SaveLocation}");
        }

        public async Task Load() {
            string obj = string.Empty;
            await Utility.FileIO(delegate {
                obj = File.ReadAllText(SaveLocation);
            });
            JsonConvert.PopulateObject(obj, this);
        }

    }

    public class FeedSet<T> : JsonSaveable {
        
        public string Name { get; set; }

        public Dictionary<T, Feed> Feeds { get; set; }

        public FeedSet(string name) {
            Feeds = new Dictionary<T, Feed>();
            Name = name;
            LoadIfFileExists();
        }

        protected override string DirectoryName => Config.FeedDirectory;
        protected override string FileName => Name;
        protected override IEnumerable<IEditable> Editables => Feeds.Values;

        public IEnumerator<Feed> GetEnumerator() { return Feeds.Values.GetEnumerator(); }

        public void Add(T key, Feed item) {
            Check.NotNull(item);
            Feeds.Add(key, item);
            Register(item);
        }

        public Feed Get(T key) {
            Feed feed;
            if (Feeds.TryGetValue(key, out feed))
                return feed;
            return null;
        }

        public void Clear() {
            if (Count <= 0)
                return;
            Feeds.Clear(); 
            Save();
        }

        public bool Contains(T key) { return Feeds.ContainsKey(key); }
        public bool Contains(Feed item) { return Feeds.ContainsValue(item); }

        public bool Remove(T key) {
            bool success = Feeds.Remove(key);
            if (success)
                Save();
            return success;
        }

        [JsonIgnore]
        public int Count => Feeds.Count;
    }

    public class GuildFeedSet : FeedSet<ulong> {
        public GuildFeedSet(DiscordSocketClient client) : base("guilds") {
            client.UserJoined += u => Get(u)?.Publish($"{u.Mention} has joined the server.");
            client.UserLeft += u => Get(u)?.Publish($"{u.Username.Bold()} has left the server.");
            client.UserBanned += (u, g) => Get(g)?.Publish($"{u.Username.Bold()} has been banned from the server.");
        }

        public Feed Get(IGuild guild) => Get(Check.NotNull(guild).Id);
        public Feed Get(IGuildUser user) => Get(Check.NotNull(user).Guild);
        public void Add(IGuild guild, Feed feed) => Add(Check.NotNull(guild).Id, feed);
    }

    public class Feed : IEditable {

        public HashSet<ulong> Channels { get; set; }
        public event Action OnEdit;

        public Feed() { Channels = new HashSet<ulong>(); }

        public async Task Publish(string message) {
            if (Bot.Client == null)
                return;
            var count = Channels.Count;
            var channels = new List<ITextChannel>();
            foreach (ulong id in Channels) {
                var channel =
                    await Bot.Client.GetChannelAsync(id) as ITextChannel;
                if (channel != null)
                    channels.Add(channel);
            }
            Channels.IntersectWith(channels.Select(ch => ch.Id));
            if(Channels.Count != count)
                OnEdit?.Invoke();
            await Task.WhenAll(channels.Select(c => c.Respond(message)));
        }

        public bool AddChannels(params ITextChannel[] channels) {
            return AddChannels(channels as IEnumerable<ITextChannel>); // Avoids infinite recursion
        }

        public bool AddChannels(IEnumerable<ITextChannel> channels) {
            var count = Channels.Count;
            Channels.UnionWith(channels.Select(ch => ch.Id));
            if (count != Channels.Count) {
                OnEdit?.Invoke();
                return true;
            }
            return false;
        }

        public bool RemoveChannels(params ITextChannel[] channels) {
            return RemoveChannels(channels as IEnumerable<ITextChannel>); // Avoids infinite recursion
        }

        public bool RemoveChannels(IEnumerable<ITextChannel> channels) {
            var count = Channels.Count;
            Channels.ExceptWith(channels.Select(ch => ch.Id));
            if (count != Channels.Count) {
                OnEdit?.Invoke();
                return true;
            }
            return false;
        }

    }

    [Module]
    [PublicOnly]
    [ModuleCheck]
    public class Feeds {

        GuildFeedSet Guilds { get; }

        public Feeds() {
            var client = Bot.Client;
            if (client == null)
                return;
            Guilds = new GuildFeedSet(client);
        }

        [Command("announce")]
        [Description("Sets the current channel to recieve bot annoucnements when a member joins, leaves, or is banned. Reusing it removes the current channel.")]
        public async Task Announce(IUserMessage messaage) {
            var channel = Check.InGuild(messaage);
            var guild = channel.Guild;
            var feed = Guilds.Get(guild);
            if(feed == null) {
                feed = new Feed();
                Guilds.Add(guild, feed);
            }
            if(feed.AddChannels(channel)) {
                await messaage.Respond($"Will now announce server changes in {channel.Mention}");
                return;
            }
            if(feed.RemoveChannels(channel)) {
                await messaage.Success($"Will no longer announce server changes in {channel.Mention}");
                return;
            }
            await messaage.Respond("An error occured.");
        }
    }
}
