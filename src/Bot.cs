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
            Client = new DiscordClient();
            Client.AddService<LogService>();
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
            Client.ServerAvailable += (s, e) => Config.GetServerConfig(e.Server);
            Client.ServerAvailable += JoinServer;

            var commandMethods =
                from assembly in AppDomain.CurrentDomain.GetAssemblies()
                from type in assembly.GetTypes()
                from method in type.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)
                where
                    method.IsDefined(typeof(CommandAttribute), false)
                        && method.IsStatic
                select method;

            var _commandGroups = new Dictionary<string, List<MethodInfo>>();

            Log.Info("Initializing commands...");
            foreach (MethodInfo method in commandMethods) {
                var groupAttribute = method.GetCustomAttribute<GroupAttribute>();
                if (groupAttribute == null) 
                    CreateCommand(method, s => CommandService.CreateCommand(s));
                else {
                    string name = groupAttribute.Name;
                    if (!_commandGroups.ContainsKey(name))
                        _commandGroups.Add(name, new List<MethodInfo>());
                    _commandGroups[name].Add(method);
                }
            }

            foreach (var commandGroup in _commandGroups) {
                CommandService.CreateGroup(commandGroup.Key,
                    cgb => {
                        foreach (MethodInfo method in commandGroup.Value)
                            CreateCommand(method, cgb.CreateCommand);
                    });
            }

            CommandService.CommandErrored += async (sender, args) => {
                string response = string.Empty;
                switch (args.ErrorType) {
                    case CommandErrorType.BadArgCount:
                        response = "Improper argument count."; 
                        break;
                    case CommandErrorType.BadPermissions:
                    case CommandErrorType.Exception:
                        if(args.Exception != null)
                            response = args.Exception.Message;
                        break;
                    case CommandErrorType.InvalidInput:
                        response = "Invalid input.";
                        break;
                    default:
                        return;
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

        void CreateCommand(MethodInfo method, Func<string, CommandBuilder> builderFunc) {
            var func = (Action<CommandEventArgs>) method.ToDelegate<Action<CommandEventArgs>>();
            if (func == null) {
                Log.Error($"Static method { method } is marked as a command but isn't compatible with the signature needed.");
                return;
            }
            var name = method.GetAttribute<CommandAttribute>().Name;
            if (string.IsNullOrEmpty(name))
                name = method.Name.ToLower();
            Log.Info($"[{name}] Creating command");
            var command = builderFunc(name);
            if (!method.IsPublic) {
                Log.Info($"[{name}] Command is not public, hiding.");
                command = command.Hide();
            }
            foreach (var builder in method.GetCustomAttributes<CommandBuilderAttribte>()) 
                command = builder.Build(name, command);
            command.Do(delegate(CommandEventArgs e) {
                Log.Info($"Command { name } was triggered by {e.User.Name} on {e.Server.Name}.");
                func(e);
            });
            Log.Info($"[{name}] Successfully created command.");
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
