using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.Modules;

namespace DrumBot {

    public class SearchModule : IModule {

        public ChannelSet ChannelSet { get; }

        const string SearchParam = "SearchTerm";

        public SearchModule(ChannelSet channels) {
            ChannelSet = Check.NotNull(channels);
        }

        Func<string, bool> ExactMatch(IEnumerable<string> matches) {
            return s => matches.All(s.Contains);
        }

        Func<string, bool> RegexMatch(string regex) {
            return s => Regex.Match(s, regex).Success;
        }

        public void Install(ModuleManager module) {
            module.CreateCommands("search", cbg => {
                cbg.Category(string.Empty);
                cbg.CreateCommand()
                    .Description("Search the history of the current channel for any of the specfied search terms.")
                    .Parameter(SearchParam, ParameterType.Multiple)
                    .Do(Search(e => ExactMatch(e.Args)));

                cbg.CreateCommand("regex")
                   .Description("Search the history of the current channel for matches to a specfied regex.")
                   .Parameter("Regex")
                   .Do(Search(e => RegexMatch(e.GetArg("Regex"))));

                cbg.CreateCommand("day")
                    .Description("Get the log of the the current channel on a certain day. Day must be of the format ``yyyy-mm-dd``")
                    .Parameter("Day")
                    .Do(GetLog);

                cbg.CreateGroup("all", a => {
                    a.CreateCommand()
                     .Description("Searches the history of all channels in the current server for any of the specfied search terms.")
                     .Parameter(SearchParam, ParameterType.Multiple)
                     .Do(SearchAll(e => ExactMatch(e.Args)));

                    a.CreateCommand("regex")
                     .Parameter("Regex")
                     .Do(SearchAll(e => RegexMatch(e.GetArg("Regex"))));
                });

                cbg.CreateGroup(i => {
                    i.AddCheck(Check.ManageChannels(bot: false));
                    i.CreateCommand("ignore")
                        .Parameter("Channel(s)", ParameterType.Multiple)
                        .Description("Mentioned channels will not be searched in ``search all``, except while in said channel. "
                            + "User must have ``Manage Channels`` permission")
                        .Do(async e => {
                           var serverConfig = Config.GetServerConfig(e.Server);
                           foreach(var channel in e.Message.MentionedChannels)
                               serverConfig.GetChannelConfig(channel).Ignore();
                            await e.Success();
                        });

                    i.CreateCommand("unignore")
                       .Parameter("Channel(s)", ParameterType.Multiple)
                       .Description("Mentioned channels will appear in ``search all`` results." 
                           +" User must have ``Manage Channels`` permission")
                       .Do(async e => {
                           var serverConfig = Config.GetServerConfig(e.Server);
                           foreach(var channel in e.Message.MentionedChannels)
                               serverConfig.GetChannelConfig(channel).Unignore();
                           await e.Success();
                        });
                });
           });
        }

        Func<CommandEventArgs,Task> Search(Func<CommandEventArgs, Func<string, bool>> pred) {
            return async delegate (CommandEventArgs e) {
                string reply = await ChannelSet.Get(e.Channel).Search(pred(e));
                await e.Respond($"Matches found in {e.Channel.Name}:\n{reply}");
            };
        }

        async Task GetLog(CommandEventArgs e) {
            string day = e.GetArg("Day");
            string path = ChannelSet.Get(e.Channel).GetPath(day);
            if (File.Exists(path))
                await e.Channel.SendFileRetry(path);
            else
                await e.Channel.SendMessage($"A log for { e.Channel.Mention } on date { day } cannot be found.");
        }

        Func<CommandEventArgs, Task> SearchAll(Func<CommandEventArgs, Func<string, bool>> pred) {
            return async delegate (CommandEventArgs e) {
                var builder = new StringBuilder();
                var server = e.Server;
                var serverConfig = Config.GetServerConfig(server);
                var serverDirectory = ChannelLog.ServerDirectory(server);
                foreach (string directory in Directory.GetDirectories(serverDirectory)) {
                    var channelName = Path.GetFileName(directory);
                    var search = ChannelLog.SearchDirectory(pred(e), directory);
                    ulong id;
                    if(ulong.TryParse(channelName, out id)) {
                        var channel = server.GetChannel(id);
                        if(channel == null || (e.Channel != channel && 
                            serverConfig.GetChannelConfig(e.Channel).IsIgnored))
                            continue;
                        channelName = channel.Name;
                    }
                    var result = await search;
                    if (string.IsNullOrEmpty(result))
                        continue;
                    builder.AppendLine($"Matches found in { channelName }");
                    builder.AppendLine(result);
                }
                await e.Respond(builder.ToString());
            };
        }
    }
}
