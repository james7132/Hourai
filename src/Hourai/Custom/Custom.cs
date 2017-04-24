using Discord;
using Discord.Commands;
using Hourai.Extensions;
using System.Threading.Tasks;

namespace Hourai.Custom {

  [RequireCustom]
  public class Custom : HouraiModule {

    [Command("dm")]
    public Task DirectMessage(IUser user, [Remainder] string message = "") =>
      user.SendDMAsync(message);

  }

}
