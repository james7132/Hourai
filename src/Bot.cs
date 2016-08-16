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
        public static ISelfUser User { get; private set; }
        IDMChannel OwnerChannel { get; set; }

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
            _initialized = false;
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
            Client.GuildAvailable += async g => Config.GetGuildConfig(g);
        }

        async Task Initialize() {
            if (_initialized)
                return;
            Log.Info("Initializing...");
            await InstallCommands(Client);
            _initialized = true;
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

        public async Task HandleMessage(IMessage msg) {
            var channel = msg.Channel as ITextChannel;
            if(!channel.AllowCommands() || msg.Author.IsBot || msg.IsAwthor())
                return;
            // Marks where the command begins
            var argPos = 0;

            // Determine if the msg is a command, based on if it starts with the defined command prefix 
            if (!msg.HasCharPrefix(Config.CommandPrefix, ref argPos))
                return;

            // Execute the command. (result does not indicate a return value, 
            // rather an object stating if the command executed succesfully)
            var result = await CommandService.Execute(msg, argPos);
            if (result.IsSuccess)
                return;
            switch (result.Error) {
                case CommandError.UnknownCommand:
                    return;
                default:
                    await msg.Respond(result.ErrorReason);
                    break;
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
        //    client.AddService(new Owner());
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

        async void SendOwnerErrors() {
            OwnerChannel = Client.GetDMChannel(Config.Owner);
            if (OwnerChannel == null)
                return;
            foreach (string error in _errors)
                await OwnerChannel.SendMessageAsync($"ERROR: {error}");
            _errors.Clear();
        }

        async Task MainLoop() {
            Log.Info("Connecting to Discord...");
            await Client.LoginAsync(TokenType.Bot, Config.Token);
            await Client.ConnectAsync();
            var self = await Client.GetCurrentUserAsync();
            Log.Info($"Logged in as { self.ToIDString() }");

            SendOwnerErrors();

            User = await Client.GetCurrentUserAsync();
            Log.Info(CommandService.Commands.Select(c => c.Text).Join(", "));
            while (true) {
                var path = Directory.GetFiles(Path.Combine(ExecutionDirectory,
                              Config.AvatarDirectory)).SelectRandom();
                await Utility.FileIO(async delegate {
                    using (var stream = new FileStream(path, FileMode.Open)) {
                        await User.ModifyAsync(u => {
                            u.Avatar = stream;
                        });
                    }
                });
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
