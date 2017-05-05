using Discord;
using Discord.Commands;
using Discord.WebSocket;
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

    public ErrorService() {
      Bot.RegularTasks += SendErrors;
      Exceptions = new List<Exception>();
    }

    async Task SendErrors() {
      if (Exceptions.Count <= 0)
        return;
      if (OwnerChannel == null)
        OwnerChannel = await Bot.Owner.CreateDMChannelAsync();
      foreach(var exception in Exceptions.ToArray()) {
        try {
          await SendError(exception);
          Exceptions.Remove(exception);
          await Task.Delay(1000);
        } catch(Exception e) {
          Log.Error(e);
          Exceptions.Add(e);
        }
      }
    }

    public async void RegisterException(Exception e) {
      if (OwnerChannel == null)
        OwnerChannel = await Bot.Owner.CreateDMChannelAsync();
      if (Config.ErrorBlacklist.Any(e.Message.Contains))
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
