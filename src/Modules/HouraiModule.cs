using Discord;
using Discord.Commands;
using System.Threading.Tasks;

namespace Hourai {

public abstract class HouraiModule : ModuleBase {

  public IGuild CurrentGuild => QCheck.InGuild(Context.Message)?.Guild;

  public Task Success(string response = "") {
    return ReplyAsync(response.IsNullOrEmpty() ? Config.SuccessResponse :
        Config.SuccessResponse + ": " + response);
  }

  public Task RespondAsync(string response) {
    return Context.Message.Respond(response);
  }

}

}
