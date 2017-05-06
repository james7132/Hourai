using Discord;
using Discord.Commands;
using Hourai.Model;
using Hourai.Custom;
using Hourai.Preconditions;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Hourai.Admin {

public partial class Admin {

  [Group("config")]
  [RequireContext(ContextType.Guild)]
  public class Configs : HouraiModule {

    public CustomConfigService ConfigService { get; set; }

    [Log]
    [Command("prefix")]
    [GuildRateLimit(1, 60)]
    [Remarks("Sets the bot's command prefix for this server.")]
    [RequirePermission(GuildPermission.ManageGuild, Require.User | Require.BotOwnerOverride)]
    public async Task Prefix([Remainder] string prefix) {
      if(string.IsNullOrEmpty(prefix)) {
        await RespondAsync("Cannot set bot prefix to an empty result");
        return;
      }
      var guild = Context.DbGuild;
      guild.Prefix = prefix.Substring(0, 1);
      await Db.Save();
      await Success($"Bot command prefix set to {guild.Prefix.ToString().Code()}");
    }

    [Command("upload")]
    [RequirePermission(GuildPermission.ManageGuild, Require.User | Require.BotOwnerOverride)]
    public async Task Upload() {
      var url = Context.Message.Attachments.Select(a => a. Url).FirstOrDefault() ??
                Context.Message.Embeds.Select(a => a.Url).FirstOrDefault();
      if (url == null) {
        await RespondAsync("No provided configuration file.");
        return;
      }
      var config = await ConfigService.GetConfig(Context.Guild);
      try {
        using (var httpClient = new HttpClient()) {
          using (var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url))) {
            var response = await httpClient.SendAsync(request);
            string content = await response.Content.ReadAsStringAsync();
            config = GuildConfig.FromString(content);
          }
        }
      } catch (Exception e) {
        await RespondAsync($"{e.GetType().Name}: {e.Message}");
        return;
      }
      await ConfigService.Save(Context.Guild, config);
    }

    [Command("dump")]
    [GuildRateLimit(1, 60)]
    public async Task Dump() =>
      await Context.Channel.SendMemoryFile($"{Context.Guild.Name}.yaml",
          (await ConfigService.GetConfig(Context.Guild)).ToString());

  }

}

}
