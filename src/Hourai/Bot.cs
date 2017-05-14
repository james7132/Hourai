using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Hourai.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Hourai {

  public class BotCounters {

    public SimpleCounter Reconnects { get; }

    public BotCounters() {
      Reconnects = new SimpleCounter();
    }

  }

  public class Bot {

    static void Main() => new Bot().RunAsync().GetAwaiter().GetResult();

    public static IUser Owner { get; private set; }

    public DateTime StartTime { get; private set; }
    public TimeSpan Uptime => DateTime.Now - StartTime;
    public string Version => Config["Version"];
    public string BotLog => Config["Logging:PathFormat"]
                              .Replace("{Date}", DateTime.Now.ToString("yyyyMMdd"));

    ILoggerFactory _loggerFactory;
    ILogger _log;
    IConfigurationRoot Config;

    DiscordBotConfig DiscordConfig { get; set; }

    DiscordShardedClient Client { get; set; }
    ErrorService ErrorService { get; set; }

    public const string BaseDataPath = "/var/bot/hourai";
#if DEBUG
    public const string ConfigFile = "test_config.json";
#else
    public const string ConfigFile = "config.json";
#endif

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
      Config = new ConfigurationBuilder()
        .SetBasePath(BaseDataPath)
        .AddJsonFile(ConfigFile)
        .Build();
      SetupLogs();
      _log.LogInformation($"Version: {Version}");
      _log.LogInformation($"Base Data Path: {BaseDataPath}");
      _log.LogInformation($"Config File Loaded: {Path.Combine(BaseDataPath, ConfigFile)}");
      _log.LogInformation($"Log File: {BotLog}");
    }

    void SetupLogs() {
      const string logStringFormat = "yyyy-MM-dd_HH_mm_ss";
      var logDirectory = Config["Storage:LogDirectory"];
      if(!Directory.Exists(logDirectory))
        Directory.CreateDirectory(logDirectory);
      _loggerFactory = new LoggerFactory()
        .AddConsole(Config.GetSection("Logging"))
        //.AddDebug()
        .AddFile(Config.GetSection("Logging"));
      _log = _loggerFactory.CreateLogger("Hourai");
    }

    public void Exit() {
      _log.LogInformation("Bot exit has registered. Will exit on next cycle.");
      ExitSource.SetResult(new object());
    }

    async Task Initialize() {
      if (_initialized)
        return;
      StartTime = DateTime.Now;
      _log.LogInformation("Initializing...");

      var discordSocketConfig = new DiscordSocketConfig();
      Config.GetSection("DiscordSocket").Bind(discordSocketConfig);

      Client = new DiscordShardedClient(discordSocketConfig);
      var commands = new CommandService(new CommandServiceConfig() {
        DefaultRunMode = RunMode.Sync
      });

      var services = new ServiceCollection();

      services.Configure<DiscordBotConfig>(Config.GetSection("Discord"));
      services.Configure<RedditConfig>(Config.GetSection("Reddit"));
      services.Configure<StorageConfig>(Config.GetSection("Storage"));

      var storageConfig = new StorageConfig();
      Config.GetSection("Storage").Bind(storageConfig);
      services.AddDbContext<BotDbContext>(options => {
        options.UseMySql(storageConfig.DbFilename);
      }, ServiceLifetime.Transient);

      services.AddSingleton(this);
      services.AddSingleton(Client);
      services.AddSingleton(commands);

      services.AddSingleton(_loggerFactory);

      services.AddSingleton(new CounterSet(new ActivatorFactory<SimpleCounter>()));
      services.AddSingleton(new BotCounters());
      services.AddSingleton<LogSet>();
      var entryAssembly = Assembly.GetEntryAssembly();

      await commands.AddModulesAsync(entryAssembly);
      await Hourai.Nitori.Touhou.BuildModule(commands, storageConfig,
            new [] { "honk", "2hu", "unyu", "9ball", "uuu",
              "alice", "awoo", "ayaya", "kappa", "mokou", "mukyu", "yuyuko",
              "zun" }, _log);

      _log.LogInformation("Loading Services...");
      var foundServices = ServiceDiscovery.FindServices(entryAssembly);
      foreach(var serviceType in foundServices) {
        services.AddSingleton(serviceType);
        _log.LogInformation($"Registered {serviceType.Name}");
      }
      var provider = new DefaultServiceProviderFactory().CreateServiceProvider(services);
      foreach(var serviceType in foundServices) {
        provider.GetService(serviceType);
        _log.LogInformation($"Loaded {serviceType.Name}");
      }
      _log.LogInformation("Services loaded.");

      ErrorService = provider.GetService<ErrorService>();
      DiscordConfig = provider.GetService<IOptions<DiscordBotConfig>>().Value;

      _initialized = true;
    }

    async Task MainLoop() {
      while (!ExitSource.Task.IsCompleted) {
        _log.LogInformation("Starting regular tasks...");
        var tasks = Task.WhenAll(_regularTasks.Select(t => t()));
        _log.LogInformation("Waiting...");
        await Task.WhenAny(Task.Delay(60000), ExitSource.Task);
      }
    }

    async Task RunAsync() {
      await Initialize();
      _log.LogInformation("Logging into Discord...");
      await Client.LoginAsync(TokenType.Bot, DiscordConfig.Token, false);
      _log.LogInformation("Starting Discord Client...");
      await Client.StartAsync();
      _log.LogInformation($"Logged in as {Client.CurrentUser.ToIDString()}");

      Owner = (await Client.GetApplicationInfoAsync()).Owner;
      _log.LogInformation($"Owner: {Owner.ToIDString()}");
      //await Client.SetGameAsync(Config.Version);
      try {
        while (!ExitSource.Task.IsCompleted) {
          try {
            await MainLoop();
          } catch (Exception error) {
            _log.LogError(0, error, "Bot error.");
            ErrorService.RegisterException(error);
          }
        }
      } finally {
        _log.LogInformation("Logging out...");
        await Client.LogoutAsync();
        _log.LogInformation("Stopping Discord client...");
        await Client.StopAsync();
      }
    }

  }

}
