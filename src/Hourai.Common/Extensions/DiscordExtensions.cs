using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace Hourai {

public static class DiscordExtensions {

  public static IEnumerable<T> EmptyIfNull<T>(this IEnumerable<T> enumerable)
    => enumerable ?? Enumerable.Empty<T>();

  public static Task<T> ToTask<T>(this T obj)
    => Task.FromResult(obj);

  /// <summary>
  /// Creates a response to a command. If the result is larger than can be retured as a single message,
  /// will upload as a text file.
  /// </summary>
  /// <param name="message">the message to respond to</param>
  /// <param name="response">the string of the message to respond with.</param>
  public static Task Respond(this IMessage message, string response) =>
    message.Channel.Respond(response);

  public static T SelectRandom<T>(this IEnumerable<T> t) {
    if (!t.Any())
      return default(T);
    IList<T> array = t as IList<T> ?? t.ToArray();
    return array[RandomUtil.Int(array.Count)];
  }

  public static Task Respond(this IMessageChannel channel, string response) {
    const string fileName = "results.txt";
    if (response == null)
      return Task.CompletedTask;
    if (response.Length > DiscordConfig.MaxMessageSize)
      return channel.SendMemoryFile(fileName, response);
    if(response.Length > 0)
      return channel.SendMessageAsync(response);
    return Task.CompletedTask;
  }

  public static Task SendFileRetry(this IMessageChannel user,
                                        string path,
                                        string text = null) {
    return Utility.FileIO(async () => {
      using (var file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
        await user.SendFileAsync(file, Path.GetFileName(path), text);
      }
    });
  }

  public static Task SendFileRetry(this IMessage message, string path, string text = null) =>
    message.Channel.SendFileRetry(path, text);

  public static async Task SendMemoryFile(this IMessageChannel channel,
                                               string name,
                                               string value,
                                               string text = null) {
    using (var stream = new MemoryStream()) {
      var writer = new StreamWriter(stream);
      writer.Write(value);
      writer.Flush();
      stream.Position = 0;
      await channel.SendFileAsync(stream, name, text);
    }
  }

  public static IEnumerable<IRole> GetRoles(this IGuildUser user) {
    return Check.NotNull(user).RoleIds.Select(r => user.Guild.GetRole(r));
  }

  /// <summary>
  /// Compares two users. Favors the channel with the higher highest role.
  /// </summary>
  public static int CompareTo(this IGuildUser u1, IGuildUser u2) {
    Func<IRole, int> rolePos = role => role.Position;
    return u1.GetRoles().Max(rolePos).CompareTo(u2.GetRoles().Max(rolePos));
  }

}

}
