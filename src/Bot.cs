using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace DrumBot {

    class Bot {
        static void Main() => new Bot().Run().GetAwaiter().GetResult();

        public static DiscordSocketClient Client { get; private set; }
        public static CommandService CommandService { get; private set; }
        public static ISelfUser User { get; private set; }
        public static CounterSet Counters { get; private set; }

        public static string ExecutionDirectory { get; private set; }
        public static string BotLog { get; private set; }

        public static DateTime StartTime { get; private set; }
        public static TimeSpan Uptime => DateTime.Now - StartTime;

        ChannelSet Channels { get; }
        IDMChannel OwnerChannel { get; set; }

        LogService LogService { get; }
        CounterService CounterService { get; }
        readonly List<string> _errors;

        const string LogStringFormat = "yyyy-MM-dd_HH_mm_ss";

        bool _initialized;

        public Bot() {
            _initialized = false;
            StartTime = DateTime.Now;
            Channels = new ChannelSet();
            Counters = new CounterSet(new ActivatorFactory<SimpleCounter>());
            _errors = new List<string>();

            ExecutionDirectory = GetExecutionDirectory();
            SetupLogs();
            Log.Info($"Execution Directory: { ExecutionDirectory }");

            Config.Load();

            Client = new DiscordSocketClient();
            LogService = new LogService(Client, Channels);
            CounterService = new CounterService(Client, Counters);
            CommandService = new CommandService();

            Log.Info($"Starting {Config.BotName}...");
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
            await CommandService.Load(new Owner(Counters));
            await CommandService.Load(new Search(Channels));
            await CommandService.Load(new Module());
            await CommandService.Load(new Help());
        }

        static string GetExecutionDirectory() {
            var uri = new UriBuilder(Assembly.GetEntryAssembly().CodeBase);
            string path = Uri.UnescapeDataString(uri.Path);
            return Path.GetDirectoryName(path);
        }

        void SetupLogs() {
            Console.OutputEncoding = Encoding.UTF8;
						var logDirectory = Path.Combine(ExecutionDirectory, Config.LogDirectory);
						if(!Directory.Exists(logDirectory))
							Directory.CreateDirectory(logDirectory);
            BotLog = Path.Combine(logDirectory, DateTime.Now.ToString(LogStringFormat) + ".log");
            Trace.Listeners.Clear();
            var botLogFile = new FileStream(BotLog, FileMode.Create, FileAccess.Write, FileShare.Read);
            Trace.Listeners.Add(new TextWriterTraceListener(botLogFile) {TraceOutputOptions = TraceOptions.ThreadId | TraceOptions.DateTime});
            Trace.Listeners.Add(new TextWriterTraceListener(Console.Out) {TraceOutputOptions = TraceOptions.DateTime});
            Trace.AutoFlush = true;
        }

        public async Task HandleMessage(IMessage m) {
            var msg = m as IUserMessage;
            if (msg == null || msg.Author.IsBot || msg.Author.IsMe())
                return;
            // Marks where the command begins
            var argPos = 0;

            // Determine if the msg is a command, based on if it starts with the defined command prefix 
            if (!msg.HasCharPrefix(Config.CommandPrefix, ref argPos))
                return;

            if (!msg.Channel.AllowCommands()) {
                Log.Info($"Attempted to run a command that is not allowed. {msg.Content.DoubleQuote()}");
                return;
            }

            // Execute the command. (result does not indicate a return value, 
            // rather an object stating if the command executed succesfully)
            var result = await CommandService.Execute(msg, argPos);
            var guildChannel = msg.Channel as ITextChannel;
            string channelMsg = guildChannel != null ? $"in {guildChannel.Name} on {guildChannel.Guild.ToIDString()}." 
                    : $"in private channel with {(await msg.Channel.GetUsersAsync()).Select(u => u.Username).Join(", ")}.";
            if (result.IsSuccess) {
                Log.Info($"Command successfully executed {msg.Content.DoubleQuote()} {channelMsg}");
                Counters.Get("command-success").Increment();
                return;
            }
            if (await CustomCommandCheck(msg, argPos))
                return;
            Log.Error($"Command failed {msg.Content.DoubleQuote()} {channelMsg} ({result.Error})");
            Counters.Get("command-failed").Increment();
            switch (result.Error) {
                // Ignore these kinds of errors, no need for response.
                case CommandError.UnknownCommand:
                    return;
                default:
                    Log.Info(result.ErrorReason);
                    await msg.Respond(result.ErrorReason);
                    break;
            }
        }

        async Task<bool> CustomCommandCheck(IMessage msg, int argPos) {
            var customCommandCheck =
                msg.Content.Substring(argPos).SplitWhitespace();
            if (customCommandCheck.Length <= 0)
                return false;
            var commandName = customCommandCheck[0];
            argPos += commandName.Length;
            var command = Config.GetGuildConfig(msg.Channel)?.GetCustomCommand(commandName);
            if (command == null)
                return false;
            await command.Execute(msg, msg.Content.Substring(argPos));
            Counters.Get("custom-command-executed").Increment();
            return true;
        }

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
            Log.Info($"Logged in as {self.ToIDString()}");

            SendOwnerErrors();

            User = await Client.GetCurrentUserAsync();
            Log.Info(CommandService.Commands.Select(c => c.Text).Join(", "));
            while (true) {
                var path = Directory.GetFiles(Path.Combine(ExecutionDirectory, Config.AvatarDirectory)).SelectRandom();
                await Utility.FileIO(async delegate {
                    using (var stream = new FileStream(path, FileMode.Open)) {
                        await User.ModifyAsync(u => { u.Avatar = stream; });
                    }
                });
                // Set the game of the bot to the bot's version.
                // TODO: Client.SetGame(Config.Version);
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
