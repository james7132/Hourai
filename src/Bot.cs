using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
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
  public static BotDbContext Database { get; private set; }

  public static string ExecutionDirectory { get; private set; }

  public static DateTime StartTime { get; private set; }
  public static TimeSpan Uptime => DateTime.Now - StartTime;

  public static CommandService CommandService { get; private set; }
  static ConcurrentDictionary<Type, object> _services;

  static TaskCompletionSource<object> ExitSource { get; set; }
  bool _initialized;

  public static event Func<Task> RegularTasks {
    add { _regularTasks.Add(Check.NotNull(value)); }
    remove { _regularTasks.Remove(value); }
  }

  static List<Func<Task>> _regularTasks;

  public Bot() {
    StartTime = DateTime.Now;
    _services = new ConcurrentDictionary<Type, object>();
    Counters = new CounterSet(new ActivatorFactory<SimpleCounter>());
    ExitSource = new TaskCompletionSource<object>();

    _regularTasks = new List<Func<Task>>();

    ExecutionDirectory = GetExecutionDirectory();
    ClientConfig = new DiscordSocketConfig();
    Client = new DiscordSocketClient(ClientConfig);
    CommandService = new CommandService();
    Add(new LogService(Client, ExecutionDirectory));
    Add(new CounterService(Client, Counters));
    Add(new BlacklistService(Client, Database));
    Add(new BotCommandService(Client, CommandService));
    Log.Info($"Execution Directory: { ExecutionDirectory }");
    Config.Load();
  }

  public static void Exit() {
    Log.Info("Bot exit has registered. Will exit on next cycle.");
    ExitSource.SetResult(new object());
  }

  async Task Initialize() {
    if (_initialized)
      return;
    Log.Info("Initializing...");
    foreach(var service in _services.Values.OfType<IService>())
      await service.Initialize();
    _initialized = true;
  }

  public static string GetExecutionDirectory() {
    var uri = new UriBuilder(Assembly.GetEntryAssembly().CodeBase);
    string path = Uri.UnescapeDataString(uri.Path);
    return Path.GetDirectoryName(path);
  }

  public static void Add<T>(T service) where T : class {
    _services[typeof(T)] = service;
  }

  public static T Get<T>() where T : class {
    return _services[typeof(T)] as T;
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

    Log.Info("Commands: " + CommandService.Commands.Select(c => c.Text).Join(", "));
    while (!ExitSource.Task.IsCompleted) {
      await Task.WhenAll(_regularTasks.Select(t => t()));
      await User.ModifyStatusAsync(u => u.Game = new Game(Config.Version));
      await Task.WhenAny(Task.Delay(60000), ExitSource.Task);
    }
  }

  public async Task Run() {
    await Initialize();
    Log.Info($"Starting...");
    using(Database = new BotDbContext()) {
      Add(new DatabaseService(Database, Client));
      while (!ExitSource.Task.IsCompleted) {
        try {
          await MainLoop();
        } catch (Exception error) {
          Log.Error(error);
        }
      }
    }
  }

}

}
