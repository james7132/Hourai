using Discord;
using Discord.WebSocket;
using Hourai.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace Hourai.Custom {

  [Service]
  public class CustomConfigExecutionService {

    readonly DiscordShardedClient _client;
    readonly CustomConfigService _config;
    readonly ErrorService _errors;
    readonly BotCommandService _commands;
    readonly ILogger _log;
    readonly IServiceProvider _services;
    public BotCommandService Commands { get; }

    public CustomConfigExecutionService(DiscordShardedClient client,
                                        CustomConfigService config,
                                        ErrorService errors,
                                        BotCommandService commands,
                                        ILoggerFactory loggerFactory,
                                        IServiceProvider services) {
      Commands = commands;
      _config = config;
      _client = client;
      _commands = commands;
      _services = services;
      _errors = errors;
      _log = loggerFactory.CreateLogger("CustomConfig");
      client.JoinedGuild += g => _config.GetConfig(g);
      client.GuildAvailable += g => _config.GetConfig(g);
      client.MessageReceived += OnMessage(g => g.OnMessage);
      client.MessageUpdated += (c, m, ch) => OnMessage(g => g.OnEdit)(m);
      client.UserJoined += u => OnUserEvent(g => g.OnJoin)(u, u.Guild);
      client.UserLeft += u => OnUserEvent(g => g.OnLeave)(u, u.Guild);
      client.UserBanned += OnUserEvent(c => c.OnBan);
    }

    static void AppendIfNotNull<T>(List<T> list, T val) where T : class {
      if (val != null) {
        list.Add(val);
      }
    }

    Task RunEvents(IEnumerable<CustomEvent> evts, HouraiContext ctx) {
      return Task.WhenAll(evts.Select(async evt => await evt.ProcessEvent(ctx, _log)));
    }

    Func<SocketMessage, Task> OnMessage(Func<DiscordContextConfig, CustomEvent> evt) {
      return async (message) => {
        var um = message as SocketUserMessage;
        var channel = message?.Channel as SocketTextChannel;
        var guild = channel?.Guild;
        if (um == null || um.Author.IsBot || guild == null) return;
        var config = await _config.GetConfig(guild);
        var evtList = new List<CustomEvent>();
        var gEvent = evt(config);
        AppendIfNotNull(evtList, evt(config));
        ChannelConfig chConfig;
        if (config.Channels != null &&
            config.Channels.TryGetValue(message.Channel.Name, out chConfig) &&
            evt(chConfig) != null) {
          AppendIfNotNull(evtList, evt(chConfig));
        }
        if (evtList.Count <= 0) return;
        _log.LogInformation("Message Custom");
        using (var db = _services.GetService<BotDbContext>()) {
          var context = new HouraiContext {
            Commands = _commands,
            Client = _client,
            Users = new[] { um.Author },
            Message = um,
            Channel = channel,
            Guild = guild,
            Db = db
          };
          foreach (var customEvent in evtList) {
            await RunEvents(evtList, context);
          }
        }
      };
    }

    Func<SocketUser, SocketGuild, Task> OnUserEvent(Func<DiscordContextConfig, CustomEvent> evt) {
      return async (user, guild) => {
        if (guild == null) return;
        var config = await _config.GetConfig(guild);
        using (var db = _services.GetService<BotDbContext>()) {
          var context = new HouraiContext {
            Commands = _commands,
            Client = _client,
            Guild = guild,
            Users = new [] { user },
            Db = db
          };
          var gEvent = evt(config);
          if (gEvent != null) await gEvent.ProcessEvent(context, _log);
          _log.LogInformation("Message Custom");
          if (config.Channels == null) return;
          foreach (var channel in config.Channels) {
            context.Channel = guild.Channels.OfType<SocketTextChannel>()
                                    .Where(ch => ch.Name == channel.Key)
                                    .FirstOrDefault();
            if (context.Channel == null) continue;
            var chEvent = evt(channel.Value);
            if (chEvent != null)
              await chEvent.ProcessEvent(context, _log);
          }
        }
      };
    }

  }

  [Service]
  public class CustomConfigService {

    readonly ConcurrentDictionary<ulong, GuildConfig> _configs;
    readonly DiscordShardedClient _client;
    readonly IServiceProvider _services;

    public CustomConfigService(DiscordShardedClient client,
                               IServiceProvider services) {
      _configs = new ConcurrentDictionary<ulong, GuildConfig>();
      _client = client;
      _services = services;
      client.LeftGuild += g => {
        GuildConfig config;
        _configs.TryRemove(g.Id, out config);
        return Task.CompletedTask;
      };
    }

    public async Task<GuildConfig> GetConfig(IGuild guild) {
      if (guild == null)
        throw new ArgumentNullException(nameof(guild));
      GuildConfig config;
      if (_configs.TryGetValue(guild.Id, out config))
        return config;
      using(var db = _services.GetService<BotDbContext>()) {
        config = await db.Configs.FindAsync(guild.Id);
        if (config == null)
          config = new GuildConfig();
        _configs[guild.Id] = config;
      }
      return config;
    }

    public async Task Save(IGuild guild, GuildConfig config) {
      if (guild == null)
        throw new ArgumentNullException(nameof(guild));
      if (config == null)
        throw new ArgumentNullException(nameof(config));
      using(var db = _services.GetService<BotDbContext>()) {
        var dbConfig = await db.Configs.Get(guild);
        dbConfig.Save(config);
        await db.Save();
        _configs[guild.Id] = config;
      }
    }

  }

}
