using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using RestSharp.Extensions;

namespace DrumBot {

    class Bot {
        public static readonly DiscordClient Client;
        public static readonly ChannelSet ChannelSet;
        public static readonly Config Config;
        public static readonly CommandService CommandService;

        static Bot() {
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
            Client = new DiscordClient(new DiscordConfigBuilder {
                
            });
            CommandService = Client.AddService(new CommandService(new CommandServiceConfigBuilder {
                    PrefixChar = Config.CommandPrefix,
                    HelpMode = HelpMode.Public
                }.Build()));
        }

        public Bot() {
            Log.Info($"Starting { Config.BotName }...");

            Client.MessageReceived +=
                async (s, e) => {
                    if (e.Message.IsAuthor)
                        return;
                    await ChannelSet.Get(e.Channel).LogMessage(e);
                };
            Client.ServerAvailable += ServerLog("Discovered");
            Client.ServerAvailable += (s, e) => Config.GetServerConfig(e.Server);
            Client.ServerAvailable += JoinServer;
            Client.ServerUnavailable += ServerLog("Lost");
            Client.ChannelCreated += ChannelLog("created");
            Client.ChannelDestroyed += ChannelLog("removed");
            Client.UserJoined += UserLog("joined");
            Client.UserLeft += UserLog("left");

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
                if(!IsMethodCompatibleWithDelegate<Action<CommandEventArgs>>(commandMethod)) {
                    Log.Error($"Static method { commandMethod } is marked as a command but isn't compatible with the signature needed.");
                }
                var name = commandMethod.GetAttribute<CommandAttribute>().Name;
                if (string.IsNullOrEmpty(name))
                    name = commandMethod.Name.ToLower();
                var func = (Action<CommandEventArgs>)Delegate.CreateDelegate(typeof(Action<CommandEventArgs>), commandMethod);
                Log.Info($"[{name}] Creating command");
                var command = CommandService.CreateCommand(name);
                if (!commandMethod.IsPublic) {
                    Log.Info($"[{name}] Command is not public, hiding.");
                    command = command.Hide();
                }
                foreach (var builder in commandMethod.GetCustomAttributes<CommandBuilderAttribte>()) 
                    command = builder.Build(name, command);
                command.Do(func);
                Log.Info($"[{name}] Successfully created command.");
            }

            CommandService.CommandErrored += async (sender, args) => {
                string response = string.Empty;
                switch (args.ErrorType) {
                    case CommandErrorType.BadArgCount:
                        response = "Improper argument count."; 
                        break;
                    case CommandErrorType.Exception:
                        response = args.Exception.Message;
                        break;
                    case CommandErrorType.InvalidInput:
                        response = "Invalid input.";
                        break;
                    default:
                        response = "Unknown error.";
                        break;
                }
                if(args.Command != null)
                    response +=
                        $" Try ``{Config.CommandPrefix}help {args.Command.Text}``.";
                else {
                    response +=
                        $" Try ``{Config.CommandPrefix}help``.";
                }
                await args.Channel.Respond(response);
            };
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

        EventHandler<ServerEventArgs> ServerLog(string eventType) {
            return delegate (object sender, ServerEventArgs e) {
                    Log.Info($"{eventType} server {e.Server.ToIDString()}. Server Count: { Client.Servers.Count() }");
            };
        }

        void JoinServer(object sender, ServerEventArgs serverEventArgs) {
            foreach (Channel channel in serverEventArgs.Server.TextChannels)
                ChannelSet.Get(channel);
        }

        async Task Login() {
            Log.Info("Connecting to Discord...");
            await Client.Connect(Config.Token);
            Log.Info($"Logged in as { Client.CurrentUser.ToIDString() }");
        }

        public void Run() {
            Client.ExecuteAndWait(Login);
        }

        static void Main() {
            new Bot().Run();
        }
    }
}
