using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Hourai {

  public class ImageStorage {

    public string Type { get; }
    public string BasePath { get; }

    static IList<string> _validImageTypes = new [] { "png", "gif", "jpg", "jpeg" };

    public ImageStorage(string type) {
      Type = Check.NotNull(type);
      Config.Load();
      BasePath = Path.Combine(Config.ImageStoragePath, type);
      Log.Info($"Storage Location for \"{type}\" images: {BasePath}");
      if (!Directory.Exists(BasePath))
        Directory.CreateDirectory(BasePath);
    }

    public async Task AddImage(ICommandContext context) {
      var link = context.Message.Attachments.FirstOrDefault()?.Url ??
                 context.Message.Embeds.FirstOrDefault()?.Url;
      if (string.IsNullOrWhiteSpace(link) || !_validImageTypes.Any(link.Contains)) {
        await context.Channel.SendMessageAsync("No vailid image found");
        return;
      }
      var uri = new Uri(link);
      string path;
      var baseName = Path.GetFileName(uri.AbsolutePath);
      Log.Info(baseName);
      do {
        string filename = baseName + "_" + Guid.NewGuid().ToString();
        path = Path.Combine(BasePath, filename);
      } while(File.Exists(path));
      using (var httpClient = new HttpClient())
      using (var request = new HttpRequestMessage(HttpMethod.Get, new Uri(link)))
      using (var response = await httpClient.SendAsync(request))
      using (var httpStream = await response.Content.ReadAsStreamAsync())
      using (var fileStream = File.Open(path, FileMode.Create, FileAccess.Write)) {
        await httpStream.CopyToAsync(fileStream);
      }
      await context.Message.Success($"Added image for {Type.Code()}");
    }

    public async Task SendImage(ICommandContext context) {
      Check.NotNull(context);
      var file = GetRandom();
      using (var fileStream = File.Open(file,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read)) {
        var fileName = Path.GetFileName(file);
        // Strip GUID from filename
        fileName = fileName.Substring(0, fileName.Length - 37);
        await context.Channel.SendFileAsync(fileStream, fileName);
      }
    }

    public string GetRandom() =>
      Directory.GetFiles(BasePath, "*", SearchOption.AllDirectories).SelectRandom();

  }

}
