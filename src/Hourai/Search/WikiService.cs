using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Hourai.Search {

  [Service]
  public class WikiService {

    readonly HttpClient _client;
    readonly ILogger _log;

    const string Endpoint = "/api.php";

    public WikiService(ILoggerFactory loggerFactory) {
      _client = new HttpClient();
      var header = new ProductInfoHeaderValue("Hourai-Discord-Bot", Config.Version);
      _client.DefaultRequestHeaders.UserAgent.Add(header);
      _log =loggerFactory.CreateLogger<WikiService>();
    }

    string AddQueryParam(string url, string param, string value) {
      return $"{url}{(url.Contains("?") ? '&' : '?')}{param}={value}";
    }

    public async Task<IEnumerable<WikiSearchResult>> SearchAsync(string urlBase, string query) {
      string url = urlBase + Endpoint;
      const int limit = 9;
      url = AddQueryParam(url, "action", "opensearch");
      url = AddQueryParam(url, "search", WebUtility.UrlEncode(query));
      url = AddQueryParam(url, "limit", limit.ToString());
      url = AddQueryParam(url, "namespace", "0");
      url = AddQueryParam(url, "format", "json");
      using(var response = await _client.GetAsync(url)) {
        var content = await response.Content.ReadAsStringAsync();
        var json = JToken.Parse(content);
        var names = json[1];
        var descriptions = json[2];
        var urls = json[3];
        var count = new[] {names.Count(), descriptions.Count(), urls.Count()}.Max();
        var results = new List<WikiSearchResult>();
        for(var i = 0; i < count; i++) {
          var result = new WikiSearchResult {
                Name = names[i].ToObject<string>(),
                Url = urls[i].ToObject<string>(),
                Description = descriptions[i].ToObject<string>()
              };
          if (string.Compare(result.Name, query, true) == 0)
            return new [] { result };
          results.Add(result);
        }
        return results;
      }
    }

  }

  public class WikiSearchResult {
    public string Name { get; set; }
    public string Url { get; set; }
    public string Description { get; set; }
  }

}
