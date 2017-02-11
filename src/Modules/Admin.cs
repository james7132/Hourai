using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Globalization;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hourai.Preconditions;

namespace Hourai.Modules {

[RequireContext(ContextType.Guild)]
[RequireModule(ModuleType.Admin)]
public partial class Admin : DatabaseHouraiModule {

  LogSet Logs { get; }
  DiscordShardedClient Client { get; }

  public Admin(DiscordShardedClient client, LogSet log, DatabaseService db) : base(db) {
    Client = client;
    Logs = log;
  }

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
  [Command("nickname")]
  [UserRateLimit(1, 1)]
  [ChannelRateLimit(4, 1)]
  [Remarks("Sets the nickname of all mentioned users, or nicknames yourself.\nIf no ``users`` is empty, nicknames the user who used the command"
  + "and requires the ``Change Nickname`` permission.\nIf at least one ``user`` is specified, nicknames the mentioned users and requires the "
  + "``Manage Nicknames`` permission.")]
  public async Task Nickname(string nickname, params IGuildUser[] users) {
    var guild = DbContext.GetGuild(Context.Guild);
    var author = Context.Message.Author as IGuildUser;
    IGuildUser[] allUsers = users;
    if (allUsers.Length <= 0) {
      if(!author.GuildPermissions.ChangeNickname) {
        await RespondAsync($"{author.Mention} you do not have the ``Change Nickname`` permission. See ``{guild.Prefix}help nickname``");
        return;
      }
      allUsers = new[] { author };
    }
    if(!author.GuildPermissions.ManageNicknames) {
      await RespondAsync($"{author.Mention} you do not have the ``Manage Nicknames`` permission. See ``{guild.Prefix}help nickname``");
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
      var guild = Check.NotNull(Context.Guild);
      var log = Logs.GetGuild(guild);
      var path =  log.GetPath(DateTimeOffset.Now);
      if(File.Exists(path))
        return Utility.FileIO(() => Context.Channel.SendFileAsync(path));
      else
        return RespondAsync("No mod events logged thus far.");
    } catch(Exception e) {
      Log.Error("hello");
      Log.Error(e);
    }
    Log.Error("Done");
    return Task.CompletedTask;
  }

  //[Group("reaction")]
  //public class Reactions : HouraiModule {

    //[Command("dump")]
    //[ChannelRateLimit(1, 1)]
    //public async Task Dump(int count = 100) {
      //var users = new HashSet<IUser>();
      //await Context.Channel.GetMessagesAsync(count).ForEachAwait(async m => {
          //foreach(var message in m.OfType<IUserMessage>()) {
            //if (!message.Reactions.Any())
              //continue;
            //foreach(var reaction in message.Reactions.Keys) {
              //var name = reaction.Name;
              //if (reaction.Id.HasValue)
                //name += ":" + reaction.Id;
              //Log.Info((await message.GetReactionUsersAsync(name)).Count);
              //Log.Info(name + " " + reaction.ToString());
            //}
          //}
        //});
      //await RespondAsync(users.Select(u => u.Mention).Join(", "));
    //}

  //}

  [Group("server")]
  public class ServerGroup : HouraiModule {

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
