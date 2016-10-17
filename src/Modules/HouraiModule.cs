using Discord;
using Discord.Commands;
using System.Threading.Tasks;

namespace Hourai {

public abstract class HouraiModule : ModuleBase {

  public Task Success(string response = "") {
    return ReplyAsync(response.IsNullOrEmpty() ? Config.SuccessResponse :
        Config.SuccessResponse + ": " + response);
  }

  public Task RespondAsync(string response) {
    return Context.Message.Respond(response);
  }

}

public abstract class DatabaseHouraiModule : HouraiModule {

  protected BotDbContext Database { get; }

  protected DatabaseHouraiModule(BotDbContext db) { 
    Database = db;
  }

}

}
