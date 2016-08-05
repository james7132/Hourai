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
using RestSharp.Extensions;

namespace DrumBot {

    class Bot {
        DiscordClient Client { get; }
        ChannelSet ChannelSet { get; }
        Channel OwnerChannel { get; set; }
        public static string ExecutionDirectory { get; private set; }
        public static string BotLog { get; private set; }
        readonly List<string> _errors;
        readonly HashSet<Type> SoftErrors;

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
            ExecutionDirectory = GetExecutionDirectory();
            BotLog = Path.Combine(ExecutionDirectory, Config.LogDirectory, DateTime.Now.ToString("yyyy-MM-dd_HH_mm_ss") + ".log");

            Trace.Listeners.Clear();
            Trace.Listeners.Add(new TextWriterTraceListener(new FileStream(BotLog, FileMode.Create, FileAccess.Write, FileShare.Read)) {
                        Name = "TextLogger",
                        TraceOutputOptions =
                            TraceOptions.ThreadId | TraceOptions.DateTime
                    });

            Trace.Listeners.Add(new ConsoleTraceListener(false) {
                TraceOutputOptions = TraceOptions.DateTime
            });
            Trace.AutoFlush = true;

            Console.OutputEncoding = Encoding.UTF8;
            Log.Info("Initializing..."); 
            ChannelSet = new ChannelSet();
            Log.Info($"Execution Directory: { ExecutionDirectory }");
            Config.Load();
            ExecuteStaticInitializers();

            SoftErrors = new HashSet<Type> {
                typeof(NotFoundException),
                typeof(RoleRankException)
            };

            Client = new DiscordClient();
            var commandService = Client.AddService(new CommandService(new CommandServiceConfigBuilder {
                PrefixChar = Config.CommandPrefix,
                HelpMode = HelpMode.Public
            }));
            Client.AddService<LogService>();
            var moduleService = Client.AddService<ModuleService>();

            Client.AddModule<ModuleModule>("Module");
            var modules = new IModule[] {
                new InfoModule(),
                new AdminModule(),
                new RoleModule(),
                new ChannelModule(),
                new SearchModule(ChannelSet), 
                new SubchannelModule()
            };
            foreach (IModule module in modules) {
                Client.AddModule(module,
                    module.GetType().Name.Replace("Module", ""),
                    ModuleFilter.ServerWhitelist);
            }
            Client.ServerAvailable += delegate(object s, ServerEventArgs e) {
                var config = Config.GetServerConfig(e.Server);
                foreach (string moduleId in config.Modules.ToArray()) {
                    var module =
                        moduleService.Modules.FirstOrDefault(
                            m => m.Id == moduleId);
                    module?.EnableServer(e.Server);
                }
            };
            foreach (var moduleManager in moduleService.Modules) {
                var id = moduleManager.Id;
                moduleManager.ServerEnabled +=
                    async delegate(object s, ServerEventArgs e) {
                        var config = Config.GetServerConfig(e.Server);
                        if(!config.IsModuleEnabled(id))
                            await config.AddModule(id);
                    };
                moduleManager.ServerDisabled +=
                    async delegate(object s, ServerEventArgs e) {
                        var config = Config.GetServerConfig(e.Server);
                        if(config.IsModuleEnabled(id))
                            await config.RemoveModule(id);
                    };
            }
            Client.AddService(new PrivateCommandService());

            // Log every public message not made by the bot.
            Client.MessageReceived +=
                async (s, e) => {
                    if (e.Message.IsAuthor || e.Channel.IsPrivate)
                        return;
                    await ChannelSet.Get(e.Channel).LogMessage(e.Message);
                };

            // Make sure that every channel is available on loading up a server.
            Client.ServerAvailable += delegate(object sender, ServerEventArgs e) {
                foreach (Channel channel in e.Server.TextChannels)
                    ChannelSet.Get(channel);
            };
            Log.Info($"Starting { Config.BotName }...");
            _errors = new List<string>();
            Client.ServerAvailable += (s, e) => Config.GetServerConfig(e.Server);

            commandService.CommandErrored += async (sender, args) => {
                string response = string.Empty;
                switch (args.ErrorType) {
                    case CommandErrorType.BadArgCount:
                        response = "Improper argument count."; 
                        break;
                    case CommandErrorType.BadPermissions:
                        if(args.Exception != null)
                            response = args.Exception.Message;
                        break;
                    case CommandErrorType.Exception:
                        if (args.Exception != null) {
                            if(SoftErrors.Contains(args.Exception.GetType())) {
                                response = args.Exception.Message;
                            } else {
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

        Channel GetOwnerChannel() {
            return Client.PrivateChannels.FirstOrDefault(
                    ch => ch.GetUser(Config.Owner) != null);
        }

        async Task MainLoop() {
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
            var startTime = DateTime.Now;
            while (true) {
                var path = Directory.GetFiles(Path.Combine(ExecutionDirectory,
                              Config.AvatarDirectory)).SelectRandom();
                await Utility.FileIO(async delegate {
                    using (var stream = new FileStream(path, FileMode.Open))
                        await Client.CurrentUser.Edit(avatar:stream, avatarType: ImageType.Png);
                });
                Client.SetGame(Config.Version);
                Log.Info($"Uptime: { DateTime.Now - startTime }");
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

        static void Main() {
            new Bot().Run();
        }
    }
}
