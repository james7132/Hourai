using Discord;
using Discord.Commands;
using Discord.Commands.Builders;
using Hourai.Nitori.GensokyoRadio;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Hourai.Nitori {

  public static class Touhou {

    const string GensokyoRadioEndpoint = "http://gensokyoradio.net/xml/";
    static XmlSerializer GensokyoRadioSerializer =
      new XmlSerializer(typeof(GensokyoRadioData));

    public static Task BuildModule(CommandService command,
                                   StorageConfig config,
                                   IEnumerable<string> imageCommands,
                                   ILogger logger) {
      return command.CreateModuleAsync("", builder => {
            builder.Name = "Touhou";
            builder.AddCommand("radio", NowPlaying, cmd => {
                  cmd.RunMode = RunMode.Async;
                  cmd.Remarks = "Pulls the currently playing song from Gensokyo Radio.";
                });
            foreach(var image in imageCommands)
              AddImageModule(command, builder, config, image, logger);
            builder.Build(command);
          });
    }

    static void AddImageModule(CommandService commands,
                               ModuleBuilder builder,
                               StorageConfig config,
                               string name,
                               ILogger logger) {
      builder.AddModule(name, module => {
        var storage = new ImageStorage(config.ImageStoragePath, name);
        logger.LogInformation($"Storing {name} images in {storage.BasePath}");
        module.AddCommand("",
            (context, param, serv) => storage.SendImage(context),
            command => {});
        module.AddCommand("add",
            (context, param, serv) => storage.AddImage(context),
            command => {
              command.AddPrecondition(new RequireOwnerAttribute());
              command.AddParameter<string>("link", param => {
                    param.IsRemainder = true;
                  });
            });
        module.Build(commands);
      });
    }

    static async Task NowPlaying(ICommandContext context,
                                object[] param,
                                IServiceProvider services) {
      GensokyoRadioData data = await GetData();
      if (data == null) {
        await context.Channel.SendMessageAsync(
            "Gensokyo Radio appears to be down, try again in a bit.");
        return;
      }
      StringBuilder output = new StringBuilder();

      var embed = new EmbedBuilder().WithUrl("https://gensokyoradio.net/");

      if (data.SongInfo.Title != "")
        embed.WithTitle($"Now playing: {data.SongInfo.Title}");

      if (data.ServerInfo.Mode != "DJ") {
          if (!string.IsNullOrWhiteSpace(data.SongInfo.Artist))
              embed.AddField("Artist", data.SongInfo.Artist);

          if (!string.IsNullOrWhiteSpace(data.SongInfo.Album)) {
            var album = data.SongInfo.Album;
            if (!string.IsNullOrWhiteSpace(data.Misc.AlbumId))
              album += $" (http://gensokyoradio.net/music/album/{data.Misc.AlbumId})";
            embed.AddField("Album", album);
          }

          var circle = new List<string>();
          if (!string.IsNullOrWhiteSpace(data.SongInfo.Circle))
            circle.Add(data.SongInfo.Circle);
          if (!string.IsNullOrWhiteSpace(data.Misc.CircleLink)) {
            var link = $"<{data.Misc.CircleLink}>";
            if (circle.Any())
              link = $"({link})";
            circle.Add(link);
          }

          if (circle.Any())
            embed.AddField("Circle", circle.Join(" "));

          if (!string.IsNullOrWhiteSpace(data.Misc.AlbumArt))
            embed.WithImageUrl(@"http://gensokyoradio.net/images/albums/200/" +
                data.Misc.AlbumArt);

          await context.Channel.SendMessageAsync(output.ToString(),
              false, embed);

          //if (data.MISC.CIRCLEART != "")
          //{
          //    if (!File.Exists(@"circles/" + data.MISC.CIRCLEART))
          //    {
          //        using (WebClient client = new WebClient())
          //        {
          //            client.DownloadFile(new Uri(@"http://gensokyoradio.net/images/circles/" + data.MISC.CIRCLEART), @"circles/" + data.MISC.CIRCLEART);
          //        };
          //    }

          //    await UploadFile(e.Channel, @"circles/" + data.MISC.CIRCLEART);
          //}
      }

    }

    static async Task<GensokyoRadioData> GetData() {
      using (var httpClient = new HttpClient())
      using (var request = new HttpRequestMessage(HttpMethod.Get, new Uri(GensokyoRadioEndpoint)))
      using (var stream = await (await httpClient.SendAsync(request)).Content.ReadAsStreamAsync())
      using (var reader = new StreamReader(stream))
      {
        var data = GensokyoRadioSerializer.Deserialize(reader) as GensokyoRadioData;

        if (data.ServerInfo.Status == "SERVICE UNAVAILABLE")
            return null;
        else
            return data;
      }
    }
  }
}
