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
      "No target specified. Please specify at least one target user.";

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

  public static Func<IGuildUser, Task<string>> Action(CommandContext context,
                                                string action,
                                                Func<IGuildUser, Task> task,
                                                bool ignoreErrors = false) {
    var guild = Check.NotNull(context.Guild);
    return async delegate (IGuildUser user) {
      string result = string.Empty;
      try {
        await task(user);
      } catch (HttpException httpException) {
        if(httpException.StatusCode == HttpStatusCode.Forbidden)
          result = "Bot has insufficient permissions.";
        else
          result = httpException.Message;
      } catch (Exception exception) {
        result = exception.Message;
      }
      if (string.IsNullOrEmpty(result) || ignoreErrors)
        return $"{user.Username}: { Config.SuccessResponse }";
      return $"{user.Username}: {result}";
    };
  }

  public static Func<IRole, Task<string>> Action(Func<IRole, Task> task,
                                                 bool ignoreErrors = false) {
    return async delegate (IRole role) {
      string result = string.Empty;
      try {
        await task(role);
      } catch (HttpException httpException) {
        if(httpException.StatusCode == HttpStatusCode.Forbidden)
          result = $"{role.Name}: Bot has insufficient permissions.";
        else
          result = httpException.Message;
      } catch (Exception exception) {
        result = exception.Message;
      }
      if (string.IsNullOrEmpty(result) || ignoreErrors)
        return $"{role.Name}: { Config.SuccessResponse }";
      return $"{role.Name}: {result}";
    };
  }

  public static Func<IGuildChannel, Task<string>> Action(Func<IGuildChannel, Task> task,
                                                       bool ignoreErrors = false) {
    return async delegate (IGuildChannel targetChannel) {
      string result = string.Empty;
      try {
        await task(targetChannel);
      } catch (Exception exception) {
        result = exception.Message;
      }
      if (string.IsNullOrEmpty(result) || ignoreErrors)
        return $"{targetChannel.Name}: { Config.SuccessResponse }";
      return $"{targetChannel.Name}: {result}";
    };
  }

}

}
