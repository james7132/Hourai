using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Hourai {

class Bot {

  static void Main() => new Bot().Run().GetAwaiter().GetResult();

  public static DiscordSocketClient Client { get; private set; }
  public static DiscordSocketConfig ClientConfig { get; private set; }
  public static ISelfUser User { get; private set; }
  public static IUser Owner { get; set; }
  public static CounterSet Counters { get; private set; }
  public static LogSet Logs { get; private set; }
  public static BotDbContext Database { get; private set; }

  public static string ExecutionDirectory { get; private set; }
  public static string BotLog { get; private set; }

  public static DateTime StartTime { get; private set; }
  public static TimeSpan Uptime => DateTime.Now - StartTime;

  IDMChannel OwnerChannel { get; set; }

  public static CommandService CommandService { get; private set; }
  LogService LogService { get; }
  CounterService CounterService { get; }
  DatabaseService DatabaseService { get; set; }

  readonly List<string> _errors;
  static bool Exited { get; set; }
  static TaskCompletionSource<object> ExitSource { get; set; }

  const string LogStringFormat = "yyyy-MM-dd_HH_mm_ss";

  bool _initialized;

  public Bot() {
    _initialized = false;
    StartTime = DateTime.Now;
    Logs = new LogSet();
    Counters = new CounterSet(new ActivatorFactory<SimpleCounter>());
    _errors = new List<string>();
    ExitSource = new TaskCompletionSource<object>();

    ExecutionDirectory = GetExecutionDirectory();
    SetupLogs();
    Log.Info($"Execution Directory: { ExecutionDirectory }");

    Config.Load();

    ClientConfig = new DiscordSocketConfig();
    Client = new DiscordSocketClient(ClientConfig);
    LogService = new LogService(Client, Logs);
    CounterService = new CounterService(Client, Counters);
    CommandService = new CommandService();
    Client.GuildAvailable += CheckBlacklist(false);
    Client.JoinedGuild += CheckBlacklist(true);

    Log.Info($"Starting...");
  }

  Func<IGuild, Task> CheckBlacklist(bool normalJoin) {
    return async guild => {
      var config = await Database.GetGuild(guild);
      var defaultChannel = (await guild.GetChannelAsync(guild.DefaultChannelId)) as ITextChannel;
      if(config.IsBlacklisted) {
        Log.Info($"Added to blacklisted guild {guild.Name} ({guild.Id})");
        await defaultChannel.Respond("This server has been blacklisted by this bot. " +
            "Please do not add it again. Leaving...");
        await guild.LeaveAsync();
        return;
      }
      if(normalJoin) {
        var help = $"{Config.CommandPrefix}help".Code();
        var module = $"{Config.CommandPrefix}module".Code();
        await defaultChannel.Respond(
            $"Hello {guild.Name}! {User.Username} has been added to your server!\n" +
            $"To see available commands, run the command {help}\n" +
            $"For more information, see https://github.com/james7132/Hourai");
      }
    };
  }

  public static void Exit() {
    Exited = true;
    Log.Info("Bot exit has registered. Will exit on next cycle.");
    ExitSource.SetResult(new object());
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
    await CommandService.Load(new Owner(Counters));
    await CommandService.LoadAssembly(Assembly.GetEntryAssembly());
    await CommandService.Load(new Help());
  }

  public static string GetExecutionDirectory() {
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
    var user = await Database.GetUser(msg.Author);
    if(user.IsBlacklisted)
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
    var customCommandCheck = msg.Content.Substring(argPos).SplitWhitespace();
    if (customCommandCheck.Length <= 0)
      return false;
    var commandName = customCommandCheck[0];
    argPos += commandName.Length;
    var guild = (msg.Channel as ITextChannel)?.Guild;
    if(guild == null)
      return false;
    var command = Bot.Database.Commands.FirstOrDefault(c => c.GuildId == guild.Id && c.Name == commandName);
    if (command == null)
      return false;
    await command.Execute(msg, msg.Content.Substring(argPos));
    Counters.Get("custom-command-executed").Increment();
    return true;
  }

  async void SendOwnerErrors() {
    OwnerChannel = Client.GetDMChannel(Owner.Id);
    if (OwnerChannel == null)
      return;
    foreach (string error in _errors)
      await OwnerChannel.SendMessageAsync($"ERROR: {error}");
    _errors.Clear();
  }

  async Task MainLoop() {
    Log.Info("Connecting to Discord...");
    await Client.LoginAsync(TokenType.Bot, Config.Token, false);
    await Client.ConnectAsync();
    var self = await Client.GetCurrentUserAsync();
    Log.Info($"Logged in as {self.ToIDString()}");

    User = await Client.GetCurrentUserAsync();
    Owner = (await Client.GetApplicationInfoAsync()).Owner;
    Log.Info($"Owner: {Owner.Username} ({Owner.Id})");

    SendOwnerErrors();

    Log.Info("Commands: " + CommandService.Commands.Select(c => c.Text).Join(", "));
    while (!Exited) {
      await User.ModifyStatusAsync(u => u.Game = new Game(Config.Version));
      await Task.WhenAny(Task.Delay(60000), ExitSource.Task);
      await Database.Save();
    }
  }

  public async Task Run() {
    await Initialize();
    using(Database = new BotDbContext()) {
      DatabaseService = new DatabaseService(Database, Client);
      while (!Exited) {
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

}
