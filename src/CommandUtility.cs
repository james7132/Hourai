using Discord;
using Discord.Net;
using Discord.Commands;
using System;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Hourai {

public static class CommandUtility {

  const string FailureResponse =
      "No target specified. Please specify at least one target.";

  public static async Task ForEvery<T>(CommandContext e,
                                       IEnumerable<T> enumeration,
                                       Func<T, string> func) {
    string[] results = enumeration.Select(func).ToArray();
    string response = results.Any()
        ? results.Join("\n")
        : FailureResponse;
    await e.Channel.Respond(response);
  }

  public static async Task ForEvery<T>(CommandContext e,
                                       IEnumerable<T> enumeration,
                                       Func<T, Task<string>> func) {
    string[] results = await Task.WhenAll(enumeration.Select(func));
    string response = results.Length > 0
        ? results.Join("\n")
        : FailureResponse;
    await e.Channel.Respond(response);
  }

  static Func<T, Task<string>> Action<T>( Func<T, Task> task,
      bool ignoreErrors,
      Func<T, string> name) {
    return async delegate (T obj) {
      string result = string.Empty;
      try {
        await task(obj);
      } catch (HttpException httpException) {
        if(httpException.StatusCode == HttpStatusCode.Forbidden)
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

  public static Func<IGuildUser, Task<string>> Action( Func<IGuildUser, Task> task,
                                                bool ignoreErrors = false)
    => Action(task, ignoreErrors, u => u.Username);

  public static Func<IRole, Task<string>> Action(Func<IRole, Task> task,
                                                 bool ignoreErrors = false)
    => Action(task, ignoreErrors, r => r.Name);

  public static Func<IGuildChannel, Task<string>> Action(Func<IGuildChannel, Task> task,
                                                       bool ignoreErrors = false)
    => Action(task, ignoreErrors, c => c.Name);

}

}
