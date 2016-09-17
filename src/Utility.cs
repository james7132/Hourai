using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord;

namespace DrumBot {

public static class Utility {

  //TODO: Expose as config option
  const int MaxRetries = 20;
  const string LogDateFormat = "yyyy-MM-dd HH:mm:ss";

  public static string Success(string followup = null) {
    var response = Config.SuccessResponse;
    if (!string.IsNullOrEmpty(followup))
      response += ": " + followup;
    return response;
  }

  public static bool RoleCheck(IGuildUser user, IRole role) {
    int position = role.Position;
    return user.IsServerOwner() || user.Roles.Max(r => r.Position) >= position;
  }

  public static string DateString(DateTime date) {
    return date.ToString(LogDateFormat);
  }

  public static string DateString(DateTimeOffset date) {
    return date.ToString(LogDateFormat);
  }

  public static async Task FileIO(Action fileIOaction,
                                  Action retry = null,
                                  Action failure = null) {
    var success = false;
    var tries = 0;
    while (!success) {
      try {
        fileIOaction();
        success = true;
      } catch (IOException) {
        if (tries <= MaxRetries) {
          retry?.Invoke();
          tries++;
          await Task.Delay(100);
        } else {
          Log.Error("Failed to perform file IO. Max retries exceeded.");
          failure?.Invoke();
          throw;
        }
      }
    }
  }

  public static async Task FileIO(Func<Task> fileIOaction,
                                  Action retry = null,
                                  Action failure = null) {
    var success = false;
    var tries = 0;
    while (!success) {
      try {
        await fileIOaction();
        success = true;
      } catch (IOException) {
        if (tries <= MaxRetries) {
          retry?.Invoke();
          tries++;
          await Task.Delay(100);
        } else {
          Log.Error("Failed to perform file IO. Max retries exceeded.");
          failure?.Invoke();
          throw;
        }
      }
    }
  }

}

}
