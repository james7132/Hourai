using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using RestSharp.Extensions;

namespace DrumBot {

    class Bot {
        public static DiscordClient Client { get; private set; }
        public static ChannelSet ChannelSet { get; private set; }
        public static Config Config { get; private set; }
        public static CommandService CommandService { get; private set; }
        public static Channel OwnerChannel { get; private set; }
        public static string ExecutionDirectory { get; private set; }
        readonly List<string> _errors;


        static string GetExecutionDirectory() {
            var uri = new UriBuilder(Assembly.GetExecutingAssembly().CodeBase);
            string path = Uri.UnescapeDataString(uri.Path);
            return Path.GetDirectoryName(path);
        }

        static void ExecuteStaticInitializers() {
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

        public Bot() {
            Log.Info("Initializing..."); 
            ChannelSet = new ChannelSet();
            ExecutionDirectory = GetExecutionDirectory();
            Log.Info($"Execution Directory: { ExecutionDirectory }");
            Config = Config.Load();
            ExecuteStaticInitializers();

            Client = new DiscordClient();
            Client.AddService<LogService>();
            CommandService = Client.AddService(new CommandService(new CommandServiceConfigBuilder {
                    PrefixChar = Config.CommandPrefix,
                    HelpMode = HelpMode.Public
                }.Build()));
            Log.Info($"Starting { Config.BotName }...");
            _errors = new List<string>();
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
                            CreateCommand(method, cgb.CreateCommand, commandGroup.Key + " ");
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
                await args.Respond(response);
            };
        }

        void CreateCommand(MethodInfo method, Func<string, CommandBuilder> builderFunc, string prefix = null) {
            var func = (Action<CommandEventArgs>) method.ToDelegate<Action<CommandEventArgs>>();
            if (func == null) {
                Log.Error($"Static method { method } is marked as a command but isn't compatible with the signature needed.");
                return;
            }
            var name = method.GetAttribute<CommandAttribute>().Name;
            if (string.IsNullOrEmpty(name))
                name = method.Name.ToLower();
            var displayName = (prefix ?? string.Empty) + name;
            Log.Info($"[{displayName}] Creating command");
            var command = builderFunc(name);
            if (!method.IsPublic) {
                Log.Info($"[{displayName}] Command is not public, hiding.");
                command = command.Hide();
            }
            foreach (var builder in method.GetCustomAttributes<CommandBuilderAttribte>()) 
                command = builder.Build(displayName, command);
            command.Do(delegate(CommandEventArgs e) {
                Log.Info($"Command { displayName } was triggered by {e.User.Name} on {e.Server.Name}.");
                func(e);
            });
            Log.Info($"[{displayName}] Successfully created command.");
        }

        void JoinServer(object sender, ServerEventArgs serverEventArgs) {
            foreach (Channel channel in serverEventArgs.Server.TextChannels)
                ChannelSet.Get(channel);
        }

        Channel GetOwnerChannel() {
            return Client.PrivateChannels.FirstOrDefault(
                    ch => ch.GetUser(Config.Owner) != null);
        }

        async Task Login() {
            Log.Info("Connecting to Discord...");
            await Client.Connect(Config.Token);
            Log.Info($"Logged in as { Client.CurrentUser.ToIDString() }");
            OwnerChannel = GetOwnerChannel();
            if (OwnerChannel == null)
                return;
            foreach (string error in _errors) 
                await OwnerChannel.SendMessage($"ERROR: {error}");
            _errors.Clear();
            await OwnerChannel.SendMessage($"Bot has been started at { DateTime.Now }");
        }

        public void Run() {
            while(true) {
                try {
                    Client.ExecuteAndWait(Login);
                } catch (Exception error) {
                    Log.Error(error);
                    _errors.Add(error.Message);
                }
            }
        }

        static void Main() {
            new Bot().Run();
        }
    }
}
