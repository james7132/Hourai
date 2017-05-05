using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Hourai.Model;
using Hourai.Preconditions;
using Hourai.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.Globalization;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Hourai.Admin {

[RequireContext(ContextType.Guild)]
public partial class Admin : HouraiModule {

  public LogSet Logs { get; set; }

  [Log]
  [Command("kick")]
  [GuildRateLimit(1, 1)]
  [RequirePermission(GuildPermission.KickMembers)]
  [Remarks("Kicks all mentioned users.")]
  public Task Kick(params IGuildUser[] users) =>
    ForEvery(users, Do(u => u.KickAsync()));

  [Log]
  [Command("ban")]
  [GuildRateLimit(1, 1)]
  [RequirePermission(GuildPermission.BanMembers)]
  [Remarks("Bans all mentioned users.")]
  public Task Ban(params IGuildUser[] users) =>
    ForEvery(users, Do(u => u.BanAsync()));

  [Log]
  [Command("ban")]
  [GuildRateLimit(1, 1)]
  [RequirePermission(GuildPermission.BanMembers)]
  [Remarks("Bans all mentioned users.")]
  public async Task Ban(params ulong[] users) {
    var guild = Context.Guild;
    await Task.WhenAll(users.Select(u => guild.AddBanAsync(u)));
    await Success();
  }

  [Log]
  [Command("softban")]
  [GuildRateLimit(1, 1)]
  [RequirePermission(GuildPermission.BanMembers)]
  [Remarks("Softbans all mentioned users.")]
  public Task Softban(params IGuildUser[] users) =>
    ForEvery(users, Do(async u => {
        ulong id = u.Id;
        await u.BanAsync(7); // Prune 7 day's worth of messages
        await u.Guild.RemoveBanAsync(id);
      }));

  [Log]
  [Command("mute")]
  [GuildRateLimit(1, 1)]
  [RequirePermission(GuildPermission.MuteMembers)]
  [Remarks("Server mutes all mentioned users.")]
  public Task Mute(params IGuildUser[] users) =>
    ForEvery(users, Do(u => u.MuteAsync()));

  [Log]
  [Command("unmute")]
  [GuildRateLimit(1, 1)]
  [RequirePermission(GuildPermission.MuteMembers)]
  [Remarks( "Server unmutes all mentioned users.")]
  public Task Unmute(params IGuildUser[] users) =>
    ForEvery(users, Do(u => u.UnmuteAsync()));

  [Log]
  [Command("deafen")]
  [GuildRateLimit(1, 1)]
  [RequirePermission(GuildPermission.DeafenMembers)]
  [Remarks( "Server deafens all mentioned users.")]
  public Task Deafen(params IGuildUser[] users) =>
    ForEvery(users, Do(u => u.DeafenAsync()));

  [Log]
  [Command("undeafen")]
  [GuildRateLimit(1, 1)]
  [RequirePermission(GuildPermission.DeafenMembers)]
  [Remarks( "Server undeafens all mentioned users.")]
  public Task Undeafen(params IGuildUser[] users) =>
    ForEvery(users, Do(u => u.UndeafenAsync()));

  [Log]
  [Command("move")]
  [GuildRateLimit(1, 1)]
  [RequirePermission(GuildPermission.MoveMembers)]
  [Remarks("Moves all users from the `src` voice channel to `dst`.")]
  public async Task Move(IVoiceChannel src, IVoiceChannel dst) =>
    await ForEvery(await src.GetUsersAsync().Flatten(), Do(u => u.ModifyAsync(x => { x.Channel = new Optional<IVoiceChannel>(dst); })));

  [Log]
  [Command("nickname")]
  [UserRateLimit(1, 1)]
  [ChannelRateLimit(4, 1)]
  [Remarks("Sets the nickname of all mentioned users, or nicknames yourself.\nIf no ``users`` is empty, nicknames the user who used the command"
  + "and requires the ``Change Nickname`` permission.\nIf at least one ``user`` is specified, nicknames the mentioned users and requires the "
  + "``Manage Nicknames`` permission.")]
  public async Task Nickname(string nickname, params IGuildUser[] users) {
    var author = Context.User as IGuildUser;
    IGuildUser[] allUsers = users;
    if (allUsers.Length <= 0) {
      if(!author.GuildPermissions.ChangeNickname) {
        await RespondAsync($"{author.Mention} you do not have the ``Change Nickname`` permission. See ``{Context.DbGuild.Prefix}help nickname``");
        return;
      }
      allUsers = new[] { author };
    }
    if(!author.GuildPermissions.ManageNicknames) {
      await RespondAsync($"{author.Mention} you do not have the ``Manage Nicknames`` permission. See ``{Context.DbGuild.Prefix}help nickname``");
      return;
    }

    await ForEvery(users, Do(async u => await u.SetNickname(nickname)));
  }

  [Command("modlog")]
  [UserRateLimit(1, 1)]
  [ChannelRateLimit(1, 1)]
  [Remarks("Gets the most recent changes on the server")]
  public Task Modlog() {
    try  {
      var log = Logs.GetGuild(Check.NotNull(Context.Guild));
      var path =  log.GetPath(DateTimeOffset.Now);
      if(File.Exists(path))
        return Utility.FileIO(() => Context.Channel.SendFileAsync(path));
      else
        return RespondAsync("No mod events logged thus far.");
    } catch(Exception e) {
      Log.LogError(0, e, "modlog failed.");
    }
    return Task.CompletedTask;
  }

  [Group("server")]
  public class Server : HouraiModule {

    [Command("permissions")]
    [UserRateLimit(1, 1)]
    [Remarks("Shows the channel permissions for one user on the current channel.\nShows your permisisons if no other user is specified")]
    public async Task Permissions(IGuildUser user = null) {
      user = user ?? (Context.Message.Author as IGuildUser);
      await RespondAsync(user.GuildPermissions.ToList()
          .Select(p => p.ToString())
          .OrderBy(s => s)
          .Join(", "));
    }

  }

}

}
