using Discord;
using Discord.Net;
using Discord.Commands;
using Hourai.Model;
using System;
using System.Net;
using System.Net.Http;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Hourai {

public abstract class HouraiModule : ModuleBase {

  const string FailureResponse =
      "No target specified. Please specify at least one target.";

  public Task Success(string response = "") {
    return ReplyAsync(response.IsNullOrEmpty() ? Config.SuccessResponse :
        Config.SuccessResponse + ": " + response);
  }

  public Task RespondAsync(string response) {
    return Context.Message.Respond(response);
  }

  protected async Task ForEvery<T>(IEnumerable<T> enumeration,
                                       Func<T, string> func) {
    string[] results = enumeration.Select(func).ToArray();
    string response = results.Any()
        ? results.Join("\n")
        : FailureResponse;
    await RespondAsync(response);
  }

  protected async Task ForEvery<T>(IEnumerable<T> enumeration,
                                       Func<T, Task<string>> func) {
    string[] results = await Task.WhenAll(enumeration.Select(func));
    string response = results.Length > 0
        ? results.Join("\n")
        : FailureResponse;
    await RespondAsync(response);
  }

  static Func<T, Task<string>> Do<T>( Func<T, Task> task,
      bool ignoreErrors,
      Func<T, string> name) {
    return async delegate (T obj) {
      string result = string.Empty;
      try {
        await task(obj);
      } catch (HttpException httpException) {
        if(httpException.HttpCode == HttpStatusCode.Forbidden)
          result = "Bot has insufficient permissions.";
        else
          result = httpException.Message;
      } catch (Exception exception) {
        result = exception.Message;
      }
      if (string.IsNullOrEmpty(result) || ignoreErrors)
        return $"{name(obj)}: { Config.SuccessResponse }";
      return $"{name(obj)}: {result}";
    };
  }

  protected static Func<IGuildUser, Task<string>> Do( Func<IGuildUser, Task> task,
                                                bool ignoreErrors = false)
    => Do(task, ignoreErrors, u => u.Username);

  protected static Func<IRole, Task<string>> Do(Func<IRole, Task> task,
                                                 bool ignoreErrors = false)
    => Do(task, ignoreErrors, r => r.Name);

  protected static Func<IGuildChannel, Task<string>> Do(Func<IGuildChannel, Task> task,
                                                       bool ignoreErrors = false)
    => Do(task, ignoreErrors, c => c.Name);

}

public abstract class DatabaseHouraiModule : HouraiModule {

  protected DatabaseService Database { get; }
  protected BotDbContext DbContext => Database.Context;

  protected DatabaseHouraiModule(DatabaseService db) {
    Database = db;
  }

}

}
