using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using RestSharp.Extensions;

namespace DrumBot {

    class DrumBot {
        readonly DiscordClient _client;
        readonly HashSet<ulong> _servers;
        readonly ChannelSet _channelSet;

        public DrumBot() {
            _client = new DiscordClient();
            _channelSet = new ChannelSet();
            _servers = new HashSet<ulong>();
            Log.Info("Starting DrumBot..");

            var config = DrumBotConfig.Instance;

            _client.MessageReceived +=
                async (s, e) => {
                    if (e.Message.IsAuthor)
                        return;
                    await _channelSet.Get(e.Channel).LogMessage(e);
                };
            _client.ServerAvailable += ServerLog("Discovered", id => _servers.Add(id));
            _client.ServerAvailable += JoinServer;
            _client.ServerUnavailable += ServerLog("Lost", id => _servers.Remove(id));
            _client.ChannelCreated += ChannelLog("created");
            _client.ChannelDestroyed += ChannelLog("removed");
            _client.UserJoined += UserLog("joined");
            _client.UserLeft += UserLog("left");

            var commandService = new CommandService(new CommandServiceConfigBuilder {
                    PrefixChar = config.CommandPrefix,
                    HelpMode = HelpMode.Public
                }.Build());

            commandService.CreateCommand("search")
                    .Alias("find", "s", "f")
                    .Description("Searches logs for certain texts.")
                    .Parameter("SearchTerm")
                    .Do(async e => {
                        Log.Info($"Command Triggered: Search by { e.User.ToIDString() }");
                        string reply = await _channelSet.Get(e.Channel).Search(e.GetArg("SearchTerm"));
                        await e.Channel.SendMessage($"{e.User.Mention}: Matches found in {e.Channel.Mention}:\n{reply}");
                    });

            //commandService.CreateCommand("avatar")
            //        .Description("Gets the avatar URLs for specified members")
            //        .Do(async e => {
            //            Log.Info($"Command Triggered: Avatar by { e.User.ToIDString() }");
            //            if(!e.Message.MentionedUsers.Any()) {
            //                await e.Channel.SendMessage("No user(s) specified. Please mention at least one user.");
            //                return;
            //            }
            //            var stringBuilder = new StringBuilder();
            //            foreach (User user in e.Message.MentionedUsers) {
            //                stringBuilder.AppendLine(user.AvatarUrl);
            //            }
            //            await e.Channel.SendMessage(stringBuilder.ToString());
            //        });

            _client.AddService(commandService);
        }

        EventHandler<UserEventArgs> UserLog(string eventType) {
            return delegate (object sender, UserEventArgs e) {
                Log.Info($"User { e.User.ToIDString() } {eventType} { e.Server.ToIDString() }");
            };
        }

        EventHandler<ChannelEventArgs> ChannelLog(string eventType) {
            return delegate (object sender, ChannelEventArgs e) {
                Log.Info($"Channel {eventType}: {e.Channel.ToIDString()} on server {e.Server.ToIDString()}");
            };
        }

        EventHandler<ServerEventArgs> ServerLog(string eventType, Func<ulong, bool> changeFunc) {
            if(changeFunc == null)
                throw new ArgumentNullException();
            return delegate (object sender, ServerEventArgs e) {
                if(changeFunc(e.Server.Id))
                    Log.Info($"{eventType} server {e.Server.ToIDString()}. Server Count: {_servers.Count}");
            };
        }

        void JoinServer(object sender, ServerEventArgs serverEventArgs) {
            foreach (Channel channel in serverEventArgs.Server.TextChannels)
                _channelSet.Get(channel);
        }

        async Task Login() {
            Log.Info("Connecting to Discord...");
            await _client.Connect(DrumBotConfig.Instance.Token);
            Log.Info($"Logged in as { _client.CurrentUser.ToIDString() }");
        }

        public void Run() {
            _client.ExecuteAndWait(async () => {
                await Login();
                _client.SetGame("James's Bongos");
            });
            _client.Disconnect();
        }

        static void StaticInitializers() {
            var types = from assembly in AppDomain.CurrentDomain.GetAssemblies()
                        from type in assembly.GetTypes()
                        where
                            !type.IsAbstract && type.IsClass
                                && type.IsDefined(
                                    typeof(InitializeOnLoadAttribute), false)
                        orderby -type.GetAttribute<InitializeOnLoadAttribute>().Order
                        select type;
            foreach(var type in types) {
                Log.Info($"Executing static initializer for { type.FullName } ({type.GetAttribute<InitializeOnLoadAttribute>().Order})...");
                RuntimeHelpers.RunClassConstructor(type.TypeHandle);
            }
        }

        static void Main(string[] args) {
            Log.Info("Initializing..."); 
            StaticInitializers();
            new DrumBot().Run();
        }
    }
}
