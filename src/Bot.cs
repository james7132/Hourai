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

  public Bot() {
    StartTime = DateTime.Now;
    _services = new ConcurrentDictionary<Type, object>();
    Counters = new CounterSet(new ActivatorFactory<SimpleCounter>());
    ExitSource = new TaskCompletionSource<object>();

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

  async Task CheckTempBans() {
    var bans = Bot.Database.TempBans.OrderByDescending(b => b.End);
    var now = DateTimeOffset.Now;
    var unbans = new List<TempBan>();
    foreach(var ban in bans) {
      Log.Info($"({ban.GuildId}, {ban.Id}): {ban.Start}, {ban.End}, {ban.End - now}");
      if(ban.End >= now)
        break;
      var guild = await Client.GetGuildAsync(ban.GuildId);
      await guild.RemoveBanAsync(ban.Id);
      unbans.Add(ban);
      Log.Info($"{ban.Id}'s temp ban from {ban.GuildId} has been lifted.");
    }
    if(unbans.Count > 0) {
      Bot.Database.TempBans.RemoveRange(unbans);
      await Bot.Database.Save();
    }
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
      await CheckTempBans();
      await User.ModifyStatusAsync(u => u.Game = new Game(Config.Version));
      await Task.WhenAny(Task.Delay(60000), ExitSource.Task);
      await Database.Save();
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
