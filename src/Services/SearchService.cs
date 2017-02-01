//using Discord;
//using Discord.Commands;
//using System;
//using System.IO;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace Hourai {

//public class SearchService {

  //LogSet Logs { get; }
  //BotDbContext Database { get; }

  //public SearchService(IDependencyMap map) {
    //Logs = map.Get<LogSet>();
    //Database = map.Get<BotDbContext>();
  //}

  ///// <summary>
  ///// Searches all logs for instances of a certain exact match.
  ///// </summary>
  ///// <returns>all matches in a string</returns>
  //public Task<string> Search(CommandContext context, Func<string, bool> pred) {
    //return SearchDirectory(context,
        //pred, Logs.GetChannel(Check.InGuild(context.Message)).SaveDirectory);
  //}

  //public Task<string> SearchAll(CommandContext context, Func<string, bool> pred) {
    //return SearchDirectory(context,
        //pred, Logs.GetGuild(context.Guild).SaveDirectory);
  //}

  //public async Task<string> SearchDirectory(CommandContext context,
      //Func<string, bool> pred,
      //string directory) {
    //if (!Directory.Exists(directory))
      //return string.Empty;
    //var guild = Check.NotNull(context.Guild);
    //var guildConfig = Database.GetGuild(guild);
    //var res =
      //from file in Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories).AsParallel()
      //from line in File.ReadLines(file)
      //where pred(line)
      //group line by Directory.GetParent(file).Name into g
      //orderby g.Key
      //select g;
    //var builder = new StringBuilder();
    //foreach (var re in res) {
      //if (!re.Any())
        //continue;
      //var name = re.Key;
      //Log.Info(name);
      //ulong id;
      //if(ulong.TryParse(name, out id)) {
        //var channel = await guild.GetChannelAsync(id);
        //if (channel != null) {
          //var config = Database.GetChannel(channel);
          //if(channel.Id != context.Channel.Id && config.SearchIgnored)
            //continue;
          //name = channel.Name;
        //}
      //}
      //builder.AppendLine($"Match results in { name }: ".Bold());
      //builder.AppendLine(re.OrderBy(s => s).Join("\n"));
    //}
    //return ChannelLog.LogToMessage(builder.ToString());
  //}

  ///// <summary>
  ///// Searches a single file for results.
  ///// </summary>
  ///// <param name="path">the path to the file</param>
  //static async Task<string> SearchFile(string path, Func<string, bool> pred) {
    //var builder = new StringBuilder();
    //Func<Task> read = async delegate {
      //using (StreamReader reader = File.OpenText(path)) {
        //while(!reader.EndOfStream) {
          //string line = await reader.ReadLineAsync();
          //if (line != null && pred(line))
            //builder.AppendLine(line);
        //}
      //}
    //};
    //Action retry = delegate { builder.Clear(); };
    //await Utility.FileIO(read, retry);
    //return builder.ToString();
  //}

//}

//}
