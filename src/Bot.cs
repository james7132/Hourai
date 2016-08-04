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
                typeof(RoleNotFoundException),
                typeof(RoleRankException)
            };

            Client = new DiscordClient();
            Client.AddService<LogService>();
            var commandService = Client.AddService(new CommandService(new CommandServiceConfigBuilder {
                    PrefixChar = Config.CommandPrefix,
                    HelpMode = HelpMode.Public
                }));
            Client.AddService<InfoService>();
            Client.AddService(new SearchService(ChannelSet));
            Client.AddService(new RoleService());
            Client.AddService(new ChannelService());
            Client.AddService(new AdminService());
            Client.AddService(new PrivateCommandService());
            //Client.AddService<SubchannelService>();

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
                await
                    Client.CurrentUser.Edit(
                        avatar:
                            new FileStream(
                                Directory.GetFiles(
                                    Path.Combine(ExecutionDirectory,
                                        Config.AvatarDirectory)).SelectRandom(),
                                FileMode.Open));
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
