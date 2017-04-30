using Discord;
using Discord.Commands;
using Hourai.Nitori.GensokyoRadio;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Hourai.Nitori {

  public class Touhou : HouraiModule {

    const string GensokyoRadioEndpoint = "http://gensokyoradio.net/xml/";
    static XmlSerializer GensokyoRadioSerializer =
      new XmlSerializer(typeof(GensokyoRadioData));

    [Command("radio", RunMode = RunMode.Async)]
    [Remarks("Pulls the currently playing song from Gensokyo Radio.")]
    public async Task NowPlaying() {
      GensokyoRadioData data = await GetData();
      if (data == null) {
        await RespondAsync("Gensokyo Radio appears to be down, try again in a bit.");
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

          await ReplyAsync(output.ToString(), false, embed);

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

    async Task<GensokyoRadioData> GetData() {
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

    [Group("honk")]
    public class Honk : HouraiModule {

      static ImageStorage storage = new ImageStorage("honk");

      [Command]
      public Task SendHonk() => storage.SendImage(Context);

      [Command("add")]
      [RequireOwner]
      public Task AddHonk([Remainder] string link) => storage.AddImage(Context);

    }

    [Group("2hu")]
    public class Twohu : HouraiModule {

      static ImageStorage storage = new ImageStorage("2hu");

      [Command]
      public Task Send() => storage.SendImage(Context);

      [Command("add")]
      [RequireOwner]
      public Task Add([Remainder] string link) => storage.AddImage(Context);

    }

    [Group("9ball")]
    public class Nineball : HouraiModule {

      static ImageStorage storage = new ImageStorage("9ball");

      [Command]
      public Task Send() => storage.SendImage(Context);

      [Command("add")]
      [RequireOwner]
      public Task Add([Remainder] string link) => storage.AddImage(Context);

    }

    [Group("alice")]
    public class Alice : HouraiModule {

      static ImageStorage storage = new ImageStorage("alice");

      [Command]
      public Task Send() => storage.SendImage(Context);

      [Command("add")]
      [RequireOwner]
      public Task Add([Remainder] string link) => storage.AddImage(Context);

    }

    [Group("awoo")]
    public class Awoo : HouraiModule {

      static ImageStorage storage = new ImageStorage("awoo");

      [Command]
      public Task Send() => storage.SendImage(Context);

      [Command("add")]
      [RequireOwner]
      public Task Add([Remainder] string link) => storage.AddImage(Context);

    }

    [Group("ayaya")]
    public class Ayaya : HouraiModule {

      static ImageStorage storage = new ImageStorage("ayaya");

      [Command]
      public Task Send() => storage.SendImage(Context);

      [Command("add")]
      [RequireOwner]
      public Task Add([Remainder] string link) => storage.AddImage(Context);

    }

    [Group("kappa")]
    public class Kappa : HouraiModule {

      static ImageStorage storage = new ImageStorage("kappa");

      [Command]
      public Task Send() => storage.SendImage(Context);

      [Command("add")]
      [RequireOwner]
      public Task Add([Remainder] string link) => storage.AddImage(Context);

    }

    [Group("mokou")]
    public class Mokou : HouraiModule {

      static ImageStorage storage = new ImageStorage("mokou");

      [Command]
      public Task Send() => storage.SendImage(Context);

      [Command("add")]
      [RequireOwner]
      public Task Add([Remainder] string link) => storage.AddImage(Context);

    }

    [Group("mukyu")]
    public class Mukyu : HouraiModule {

      static ImageStorage storage = new ImageStorage("mukyu");

      [Command]
      public Task Send() => storage.SendImage(Context);

      [Command("add")]
      [RequireOwner]
      public Task Add([Remainder] string link) => storage.AddImage(Context);

    }

    [Group("unyu")]
    public class Unyu : HouraiModule {

      static ImageStorage storage = new ImageStorage("unyu");

      [Command]
      public Task Send() => storage.SendImage(Context);

      [Command("add")]
      [RequireOwner]
      public Task Add([Remainder] string link) => storage.AddImage(Context);

    }

    [Group("uuu")]
    public class Uuu : HouraiModule {

      static ImageStorage storage = new ImageStorage("uuu");

      [Command]
      public Task Send() => storage.SendImage(Context);

      [Command("add")]
      [RequireOwner]
      public Task Add([Remainder] string link) => storage.AddImage(Context);

    }

    [Group("yuyuko")]
    public class Yuyuko : HouraiModule {

      static ImageStorage storage = new ImageStorage("yuyuko");

      [Command]
      public Task Send() => storage.SendImage(Context);

      [Command("add")]
      [RequireOwner]
      public Task Add([Remainder] string link) => storage.AddImage(Context);

    }

    [Group("zun")]
    public class Zun : HouraiModule {

      static ImageStorage storage = new ImageStorage("zun");

      [Command]
      public Task Send() => storage.SendImage(Context);

      [Command("add")]
      [RequireOwner]
      public Task Add([Remainder] string link) => storage.AddImage(Context);

    }
  }



}
