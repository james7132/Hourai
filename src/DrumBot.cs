using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using RestSharp.Extensions;

namespace DrumBot {

    class DrumBot {
        readonly DiscordClient _client;
        readonly HashSet<ulong> _servers;
        public static readonly ChannelSet ChannelSet;
        public static readonly Config Config;

        static DrumBot() {
            Log.Info("Initializing..."); 
            ChannelSet = new ChannelSet();
            Config = Config.Load();
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

        public DrumBot() {
            _client = new DiscordClient();
            _servers = new HashSet<ulong>();
            Log.Info($"Starting { Config.BotName }..");

            _client.MessageReceived +=
                async (s, e) => {
                    if (e.Message.IsAuthor)
                        return;
                    await ChannelSet.Get(e.Channel).LogMessage(e);
                };
            _client.ServerAvailable += ServerLog("Discovered", id => _servers.Add(id));
            _client.ServerAvailable += (s, e) => Config.GetServerConfig(e.Server);
            _client.ServerAvailable += JoinServer;
            _client.ServerUnavailable += ServerLog("Lost", id => _servers.Remove(id));
            _client.ChannelCreated += ChannelLog("created");
            _client.ChannelDestroyed += ChannelLog("removed");
            _client.UserJoined += UserLog("joined");
            _client.UserLeft += UserLog("left");

            var commandService = new CommandService(new CommandServiceConfigBuilder {
                    PrefixChar = Config.CommandPrefix,
                    HelpMode = HelpMode.Public
                }.Build());

            var commandMethods =
                from assembly in AppDomain.CurrentDomain.GetAssemblies()
                from type in assembly.GetTypes()
                from method in type.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)
                where
                    method.IsDefined(typeof(CommandAttribute), false)
                        && method.IsStatic
                select method;

            Log.Info("Initializing commands...");
            foreach (MethodInfo commandMethod in commandMethods) {
                if(!IsMethodCompatibleWithDelegate<Func<CommandEventArgs, Task>>(commandMethod)) {
                    Log.Error($"Static method { commandMethod } is marked as a command but isn't compatible with the signature needed.");
                }
                var name = commandMethod.GetAttribute<CommandAttribute>().Name;
                var func = (Func<CommandEventArgs, Task>)Delegate.CreateDelegate(typeof(Func<CommandEventArgs, Task>), commandMethod);
                Log.Info($"Creating command \"{name}\"");
                var command = commandService.CreateCommand(name);
                if (!commandMethod.IsPublic) {
                    Log.Info($"Command \"{ name }\" is not public, hiding.");
                    command = command.Hide();
                }
                foreach (var builder in commandMethod.GetCustomAttributes<CommandBuilderAttribte>()) 
                    command = builder.Build(name, command);
                foreach(var decorator in commandMethod.GetCustomAttributes<CommandDecoratorAttribute>())
                    func = decorator.Decorate(name, func);
                command.Do(func);
                Log.Info($"Successfully created command \"{name}\"");
            }

            _client.AddService(commandService);
        }

        bool IsMethodCompatibleWithDelegate<T>(MethodInfo method) where T : class {
            Type delegateType = typeof(T);
            MethodInfo delegateSignature = delegateType.GetMethod("Invoke");

            bool parametersEqual = delegateSignature
                .GetParameters()
                .Select(x => x.ParameterType)
                .SequenceEqual(method.GetParameters()
                    .Select(x => x.ParameterType));

            return delegateSignature.ReturnType == method.ReturnType &&
                   parametersEqual;
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
                ChannelSet.Get(channel);
        }

        async Task Login() {
            Log.Info("Connecting to Discord...");
            await _client.Connect(Config.Token);
            Log.Info($"Logged in as { _client.CurrentUser.ToIDString() }");
        }

        public void Run() {
            _client.ExecuteAndWait(async () => {
                await Login();
            });
        }

        static void Main(string[] args) {
            new DrumBot().Run();
        }
    }
}
