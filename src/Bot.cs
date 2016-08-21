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
using Discord.Modules;
using DrumBot.src.Services;

namespace DrumBot {

    class Bot {
        DiscordClient Client { get; }
        Channel OwnerChannel { get; set; }
        public static string ExecutionDirectory { get; private set; }
        public static string BotLog { get; private set; }
        readonly List<string> _errors;
        readonly HashSet<Type> _softErrors;
        readonly DateTime _startTime;

        const string LogStringFormat = "yyyy-MM-dd_HH_mm_ss";

        public Bot() {
            _startTime = DateTime.Now;
            var channelSet = new ChannelSet();
            _errors = new List<string>();
            _softErrors = new HashSet<Type> {
                typeof(NotFoundException),
                typeof(RoleRankException)
            };

            ExecutionDirectory = GetExecutionDirectory();
            SetupLogs();

            Log.Info("Initializing..."); 
            Log.Info($"Execution Directory: { ExecutionDirectory }");
            Config.Load();
            ExecuteStaticInitializers();

            Client = new DiscordClient();
            AddCommands(Client);
            AddModules(Client, channelSet);
            Client.AddService(new LogService(channelSet));

            Log.Info($"Starting { Config.BotName }...");
            Client.ServerAvailable += (s, e) => Config.GetServerConfig(e.Server);
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
            foreach(var type in types) {
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

        ModuleService AddModules(DiscordClient client, ChannelSet channels) {
            var moduleService = client.AddService<ModuleService>();
            client.AddModule<ModuleModule>("Module");
            var modules = from type in ReflectionUtility.ConcreteClasses
                                           .InheritsFrom<IModule>()
                                           .WithParameterlessConstructor()
                          where type != typeof(ModuleModule)
                          select Activator.CreateInstance(type) as IModule;
            modules = modules.Concat(new IModule[] {
                new SearchModule(channels), 
            });
            foreach (IModule module in modules) {
                var moduleName = module.GetType().Name.Replace("Module", "");
                Log.Info($"Adding module {moduleName.DoubleQuote()}");
                client.AddModule(module, moduleName, ModuleFilter.ServerWhitelist);
            }

            // Enable modules for each server based on what is saved in the configs.
            Client.ServerAvailable += delegate(object s, ServerEventArgs e) {
                var config = Config.GetServerConfig(e.Server);
                foreach (string moduleId in config.Modules.ToArray()) {
                    var module = moduleService.Modules.FirstOrDefault(m => m.Id == moduleId);
                    module?.EnableServer(e.Server);
                }
            };
            
            // Set up modules to save when each server enables/disables a server.
            foreach (var moduleManager in moduleService.Modules) {
                var id = moduleManager.Id;
                moduleManager.ServerEnabled += delegate(object s, ServerEventArgs e) {
                        var config = Config.GetServerConfig(e.Server);
                        if(!config.IsModuleEnabled(id))
                            config.AddModule(id);
                    };
                moduleManager.ServerDisabled += delegate(object s, ServerEventArgs e) {
                        var config = Config.GetServerConfig(e.Server);
                        if(config.IsModuleEnabled(id))
                            config.RemoveModule(id);
                    };
            }
            return moduleService;
        }

        CommandService AddCommands(DiscordClient client) {
            // Short stub to calculate the standard prefix location.
            Func<string, int> defaultPrefix = s => s[0] == Config.CommandPrefix ? 1 : -1;
            var commandService = client.AddService(new CommandService(new CommandServiceConfigBuilder {
                HelpMode = HelpMode.Public,
                // Use prefix handler to filter out non-production servers while testing.
                CustomPrefixHandler = delegate (Message message) {
                    string msg = message.RawText;
                    if (message.Channel.IsPrivate)
                        return defaultPrefix(msg);
                    return Config.GetServerConfig(message.Server).AllowCommands ? defaultPrefix(msg) : -1;
                }
            }));
            commandService.CommandErrored += OnCommandError;
            client.AddService(new BotOwnerCommandService());
            return commandService;
        }

        async void OnCommandError(object sender, CommandErrorEventArgs args) {
            string response = string.Empty;
            switch (args.ErrorType) {
                case CommandErrorType.BadArgCount:
                    response = "Improper argument count.";
                    break;
                case CommandErrorType.BadPermissions:
                    if (args.Exception != null)
                        response = args.Exception.Message;
                    break;
                case CommandErrorType.Exception:
                    if (args.Exception != null) {
                        if (_softErrors.Contains(args.Exception.GetType())) {
                            response = args.Exception.Message;
                        }
                        else {
                            Log.Error(args.Exception);
                            response = args.Exception.ToString().MultilineCode();
                        }
                    }
                    break;
                case CommandErrorType.InvalidInput:
                    response = "Invalid input.";
                    break;
                default:
                    return;
            }
            if (string.IsNullOrEmpty(response))
                return;
            if (args.Command != null)
                response += $" Try ``{Config.CommandPrefix}help {args.Command.Text}``.";
            else {
                response += $" Try ``{Config.CommandPrefix}help``.";
            }
            await args.Respond(response);
        }

        Channel GetOwnerChannel() {
            return Client.PrivateChannels.FirstOrDefault(ch => ch.GetUser(Config.Owner) != null);
        }

        async void SendOwnerErrors() {
            OwnerChannel = GetOwnerChannel();
            if (OwnerChannel == null)
                return;
            foreach (string error in _errors) 
                await OwnerChannel.SendMessage($"ERROR: {error}");
            _errors.Clear();
        }

        async Task MainLoop() {
            Log.Info("Connecting to Discord...");
            await Client.Connect(Config.Token);
            Log.Info($"Logged in as { Client.CurrentUser.ToIDString() }");

            SendOwnerErrors();

            while (true) {
                // Select random avatar to set to the bot
                var path = Directory.GetFiles(Path.Combine(ExecutionDirectory,
                              Config.AvatarDirectory)).SelectRandom();
                try {
                    await Utility.FileIO(async delegate {
                        using (var stream = new FileStream(path, FileMode.Open))
                            await Client.CurrentUser.Edit(avatar:stream);
                    });
                } catch {
                    Log.Error("Failed to change avatar. Continuing...");
                }

                // Set the game of the bot to the bot's version.
                Client.SetGame(Config.Version);

                // Log uptime
                Log.Info($"Uptime: {DateTime.Now - _startTime}");
                await Task.Delay(300000);
            }
        }

        public void Run() {
            while(true) {
                try {
                    Client.ExecuteAndWait(MainLoop);
                } catch (Exception error) {
                    Log.Error(error);
                    _errors.Add(error.Message);
                }
            }
        }

        static void Main() => new Bot().Run();
    }
}
