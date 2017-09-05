using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Hourai {

  [Service]
  public class ErrorService {

    public Bot Bot { get; set; }
    IMessageChannel OwnerChannel { get; set; }
    List<Exception> Exceptions { get; }
    readonly DiscordBotConfig _config;
    readonly ILogger _log;

    public ErrorService(IOptions<DiscordBotConfig> config,
                        ILoggerFactory loggerFactory) {
      Bot.RegularTasks += SendErrors;
      Exceptions = new List<Exception>();
      _log = loggerFactory.CreateLogger<ErrorService>();
      _config = config.Value;
      foreach (var blacklistedError in _config.ErrorBlacklist)
        _log.LogInformation($"Error Blacklist: \"{blacklistedError}\"");
    }

    async Task SendErrors() {
      if (Exceptions.Count <= 0)
        return;
      if (OwnerChannel == null)
        OwnerChannel = await Bot.Owner.GetOrCreateDMChannelAsync();
      foreach(var exception in Exceptions.ToArray()) {
        try {
          await SendError(exception);
          Exceptions.Remove(exception);
          await Task.Delay(1000);
        } catch(Exception e) {
          _log.LogError(0, e, "Error sending errors to owner.");
          Exceptions.Add(e);
        }
      }
    }

    public async void RegisterException(Exception e) {
      if (OwnerChannel == null)
        OwnerChannel = await Bot.Owner.GetOrCreateDMChannelAsync();
      if (_config.ErrorBlacklist.Any(e.ToString().Contains))
        return;
      try {
        await SendError(e);
      } catch {
        Exceptions.Add(e);
      }
    }

    Task SendError(Exception exception) {
      return OwnerChannel.SendMessageAsync(exception.ToString().Ellipisize(400).Code());
    }

  }


}
