using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace DrumBot {

    class Bot {
        static void Main() => new Bot().Run().GetAwaiter().GetResult();
        public static DiscordSocketClient Client { get; private set; }
        public static ChannelSet Channels { get; private set; }
        IChannel OwnerChannel { get; set; }

        public static CommandService CommandService { get; private set; }
        LogService LogService { get; }
        public static string ExecutionDirectory { get; private set; }
        public static string BotLog { get; private set; }
        readonly List<string> _errors;
        readonly HashSet<Type> _softErrors;
        readonly DateTime _startTime;

        const string LogStringFormat = "yyyy-MM-dd_HH_mm_ss";

        bool _initialized;

        public Bot() {
            _startTime = DateTime.Now;
            Channels = new ChannelSet();
            _errors = new List<string>();
            _softErrors = new HashSet<Type> {
                typeof(NotFoundException),
                typeof(RoleRankException)
            };

            ExecutionDirectory = GetExecutionDirectory();
            SetupLogs();
            Log.Info($"Execution Directory: { ExecutionDirectory }");

            Config.Load();
            ExecuteStaticInitializers();

            Client = new DiscordSocketClient();
            CommandService = new CommandService();
            LogService = new LogService(Channels);

            Log.Info($"Starting { Config.BotName }...");
            //TODO: Client.ServerAvailable += (s, e) => Config.GetGuildConfig(e.Server);
        }

        async Task Initialize() {
            if (_initialized)
                return;
            Log.Info("Initializing...");
            await InstallCommands(Client);
        }

        async Task InstallCommands(DiscordSocketClient client) {
            client.MessageReceived += HandleMessage;
            await CommandService.LoadAssembly(Assembly.GetEntryAssembly());
            await CommandService.Load(new Search(Channels));
            await CommandService.Load(new Help());
        }

        static string GetExecutionDirectory() {
            var uri = new UriBuilder(Assembly.GetExecutingAssembly().CodeBase);
            string path = Uri.UnescapeDataString(uri.Path);
            return Path.GetDirectoryName(path);
        }

        static void ExecuteStaticInitializers() {
            var types = from type in ReflectionUtility.ConcreteClasses
                                    .WithAttribute<InitializeOnLoadAttribute>()
                        orderby -type.Value.Order
                        select type;
            foreach (var type in types) {
                Log.Info($"Executing static initializer for { type.Key.FullName } ({type.Value.Order})...");
                RuntimeHelpers.RunClassConstructor(type.Key.TypeHandle);
            }
        }

        void SetupLogs() {
            Console.OutputEncoding = Encoding.UTF8;
            BotLog = Path.Combine(ExecutionDirectory, Config.LogDirectory, DateTime.Now.ToString(LogStringFormat) + ".log");
            Trace.Listeners.Clear();
            var botLogFile = new FileStream(BotLog,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read);
            Trace.Listeners.Add(new TextWriterTraceListener(botLogFile) {
                TraceOutputOptions = TraceOptions.ThreadId | TraceOptions.DateTime
            });
            Trace.Listeners.Add(new ConsoleTraceListener(false) {
                TraceOutputOptions = TraceOptions.DateTime
            });
            Trace.AutoFlush = true;
        }

        //ModuleService AddModules(DiscordClient client, ChannelSet channels) {
        //    var moduleService = client.AddService<ModuleService>();
        //    client.AddModule<Module>("Module");
        //    var modules = from type in ReflectionUtility.ConcreteClasses
        //                                   .InheritsFrom<IModule>()
        //                                   .WithParameterlessConstructor()
        //                  where type != typeof(Module)
        //                  select Activator.CreateInstance(type) as IModule;
        //    modules = modules.Concat(new IModule[] {
        //        new SearchChat(channels),
        //    });
        //    foreach (IModule module in modules) {
        //        var moduleName = module.GetType().Name.Replace("Module", "");
        //        Log.Info($"Adding module {moduleName.DoubleQuote()}");
        //        client.AddModule(module, moduleName, ModuleFilter.ServerWhitelist);
        //    }

        //    // Enable modules for each server based on what is saved in the configs.
        //    Client.ServerAvailable += delegate(object s, ServerEventArgs e) {
        //        var config = Config.GetGuildConfig(e.Server);
        //        foreach (string moduleId in config.Modules.ToArray()) {
        //            var module = moduleService.Modules.FirstOrDefault(m => m.Id == moduleId);
        //            module?.EnableServer(e.Server);
        //        }
        //    };

            //    // Set up modules to save when each server enables/disables a server.
            //    foreach (var moduleManager in moduleService.Modules) {
            //        var id = moduleManager.Id;
            //        moduleManager.ServerEnabled += delegate(object s, ServerEventArgs e) {
            //                var config = Config.GetGuildConfig(e.Server);
            //                if(!config.IsModuleEnabled(id))
            //                    config.AddModule(id);
            //            };
            //        moduleManager.ServerDisabled += delegate(object s, ServerEventArgs e) {
            //                var config = Config.GetGuildConfig(e.Server);
            //                if(config.IsModuleEnabled(id))
            //                    config.RemoveModule(id);
            //            };
            //    }
            //    return moduleService;
            //}

        public async Task HandleMessage(IMessage msg) {
            Log.Info(msg.Content);
            // Marks where the command begins
            var argPos = 0;
            // Determine if the msg is a command, based on if it starts with the defined command prefix 
            if (msg.HasCharPrefix(Config.CommandPrefix, ref argPos)) {
                // Execute the command. (result does not indicate a return value, 
                // rather an object stating if the command executed succesfully)
                var result = await CommandService.Execute(msg, argPos);
                if (!result.IsSuccess)
                    await msg.Channel.SendMessageAsync(result.ErrorReason);
            }
        }

        //CommandService AddCommands(DiscordClient client) {
        //    // Short stub to calculate the standard prefix location.
        //    Func<string, int> defaultPrefix = s => s[0] == Config.CommandPrefix ? 1 : -1;
        //    var commandService = client.AddService(new CommandService(new CommandServiceConfigBuilder {
        //        HelpMode = HelpMode.Public,
        //        // Use prefix handler to filter out non-production servers while testing.
        //        CustomPrefixHandler = delegate (Message message) {
        //            string msg = message.RawText;
        //            if (message.Channel.IsPrivate)
        //                return defaultPrefix(msg);
        //            return Config.GetGuildConfig(message.Server).AllowCommands ? defaultPrefix(msg) : -1;
        //        }
        //    }));
        //    commandService.CommandErrored += OnCommandError;
        //    client.AddService(new BotOwnerModule());
        //    return commandService;
        //}

        //async void OnCommandError(object sender, CommandErrorEventArgs args) {
        //    string response = string.Empty;
        //    switch (args.ErrorType) {
        //        case CommandErrorType.BadArgCount:
        //            response = "Improper argument count.";
        //            break;
        //        case CommandErrorType.BadPermissions:
        //            if (args.Exception != null)
        //                response = args.Exception.Message;
        //            break;
        //        case CommandErrorType.Exception:
        //            if (args.Exception != null) {
        //                if (_softErrors.Contains(args.Exception.GetType())) {
        //                    response = args.Exception.Message;
        //                }
        //                else {
        //                    Log.Error(args.Exception);
        //                    response = args.Exception.ToString().MultilineCode();
        //                }
        //            }
        //            break;
        //        case CommandErrorType.InvalidInput:
        //            response = "Invalid input.";
        //            break;
        //        default:
        //            return;
        //    }
        //    if (string.IsNullOrEmpty(response))
        //        return;
        //    if (args.CommandUtility != null)
        //        response += $" Try ``{Config.CommandPrefix}help {args.CommandUtility.Text}``.";
        //    else {
        //        response += $" Try ``{Config.CommandPrefix}help``.";
        //    }
        //    await args.Respond(response);
        //}

        //Channel GetOwnerChannel() {
        //    return Client.PrivateChannels.FirstOrDefault(ch => ch.GetUser(Config.Owner) != null);
        //}

        //async void SendOwnerErrors() {
        //    OwnerChannel = GetOwnerChannel();
        //    if (OwnerChannel == null)
        //        return;
        //    foreach (string error in _errors) 
        //        await OwnerChannel.SendMessage($"ERROR: {error}");
        //    _errors.Clear();
        //}

        async Task MainLoop() {
            Log.Info("Connecting to Discord...");
            await Client.LoginAsync(TokenType.Bot, Config.Token);
            await Client.ConnectAsync();
            var self = await Client.GetCurrentUserAsync();
            Log.Info($"Logged in as { self.ToIDString() }");

            //SendOwnerErrors();

            while (true) {
                Log.Info(CommandService.Commands.Select(c => c.Text).Join(", "));
                // TODO: Select random avatar to set to the bot
                //var path = Directory.GetFiles(Path.Combine(ExecutionDirectory,
                //              Config.AvatarDirectory)).SelectRandom();
                //await Utility.FileIO(async delegate {
                //    using (var stream = new FileStream(path, FileMode.Open))
                //        await Client.CurrentUser.Edit(avatar:stream);
                //});

                // Set the game of the bot to the bot's version.
                // TODO: Client.SetGame(Config.Version);

                // Log uptime
                Log.Info($"Uptime: {DateTime.Now - _startTime}");
                await Task.Delay(300000);
            }
        }

        public async Task Run() {
            await Initialize();
            while (true) {
                try {
                    await MainLoop();
                } catch (Exception error) {
                    Log.Error(error);
                    _errors.Add(error.Message);
                }
            }
        }

    }
}
