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

public class Bot {

  static void Main() => new Bot().Run().GetAwaiter().GetResult();

  public static ISelfUser User { get; private set; }
  public static IUser Owner { get; set; }

  public static string ExecutionDirectory { get; private set; }

  public DateTime StartTime { get; private set; }
  public TimeSpan Uptime => DateTime.Now - StartTime;

  DiscordSocketClient Client { get; set; }

  static TaskCompletionSource<object> ExitSource { get; set; }
  bool _initialized;

  public static event Func<Task> RegularTasks {
    add { _regularTasks.Add(Check.NotNull(value)); }
    remove { _regularTasks.Remove(value); }
  }

  static List<Func<Task>> _regularTasks;

  public Bot() {
    ExitSource = new TaskCompletionSource<object>();

    _regularTasks = new List<Func<Task>>();

    ExecutionDirectory = GetExecutionDirectory();
    Log.Info($"Execution Directory: { ExecutionDirectory }");
    Config.Load();
  }

  public static void Exit() {
    Log.Info("Bot exit has registered. Will exit on next cycle.");
    ExitSource.SetResult(new object());
  }

  async Task Initialize(BotDbContext db) {
    if (_initialized)
      return;
    StartTime = DateTime.Now;
    Log.Info("Initializing...");
    var config = new DiscordSocketConfig();
    Client = new DiscordSocketClient(config);
    var CommandService = new CommandService();
    var map = new DependencyMap();
    map.Add(this);
    map.Add(map);
    map.Add(Client);

    map.Add(db);
    map.Add(new CounterSet(new ActivatorFactory<SimpleCounter>()));
    map.Add(new LogSet());

    map.Add(new DatabaseService(map));

    map.Add(new LogService(map, ExecutionDirectory));
    map.Add(new CounterService(map));

    map.Add(CommandService);
    await CommandService.AddModules(Assembly.GetEntryAssembly());
    map.Add(new BotCommandService(map));

    map.Add(new TempService(map));
    map.Add(new BlacklistService(map));
    map.Add(new AnnounceService(map));
    map.Add(new SearchService(map));

    _initialized = true;
  }

  public static string GetExecutionDirectory() {
    var uri = new UriBuilder(Assembly.GetEntryAssembly().CodeBase);
    string path = Uri.UnescapeDataString(uri.Path);
    return Path.GetDirectoryName(path);
  }

  async Task MainLoop() {
    Log.Info("Connecting to Discord...");
    await Client.LoginAsync(TokenType.Bot, Config.Token, false);
    await Client.ConnectAsync();
    User = Client.CurrentUser;
    Log.Info($"Logged in as {User.ToIDString()}");

    Owner = (await Client.GetApplicationInfoAsync()).Owner;
    Log.Info($"Owner: {Owner.Username} ({Owner.Id})");
    while (!ExitSource.Task.IsCompleted) {
      await Task.WhenAll(_regularTasks.Select(t => t()));
      await Client.SetGame(Config.Version); 
      await Task.WhenAny(Task.Delay(60000), ExitSource.Task);
    }
  }

  async Task Run() {
    Log.Info($"Starting...");
    using(var database = new BotDbContext()) {
      await Initialize(database);
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
