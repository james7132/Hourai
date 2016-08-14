using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace DrumBot {

    [Module("search", AutoLoad = false)]
    public class Search {

        public ChannelSet ChannelSet { get; }

        public Search(ChannelSet channels) {
            ChannelSet = Check.NotNull(channels);
        }

        Func<string, bool> ExactMatch(IEnumerable<string> matches) {
            return s => matches.All(s.Contains);
        }

        Func<string, bool> RegexMatch(string regex) {
            return new Regex(regex, RegexOptions.Compiled).IsMatch;
        }

        [Command]
        [Description("Search the history of the current channel for messages that match all of the specfied search terms.")]
        public async Task SearchChat(IMessage message, params string[] terms) {
            await SearchChannel(message, ExactMatch(terms));
        }

        [Command("regex")]
        [Description("Search the history of the current channel for matches to a specfied regex.")]
        public async Task SearchRegex(IMessage message, string regex) {
            await SearchChannel(message, RegexMatch(regex));
        }

        [Command("day")]
        [Description("SearchChat the log of the the current channel on a certain day. Day must be of the format ``yyyy-mm-dd``")]
        public async Task Day(IMessage message, string day) {
            var channel = Check.InGuild(message);
            string path = ChannelSet.Get(channel).GetPath(day);
            if (File.Exists(path))
                await message.SendFileRetry(path);
            else
                await message.Respond($"A log for {channel.Mention} on date {day} cannot be found.");
        }

        [Command("all")]
        [Description("Searches the history of all channels in the current server for any of the specfied search terms.")]
        public async Task All(IMessage message, params string[] terms) {
            await SearchAll(message, ExactMatch(terms));
        }

        [Command("all regex")]
        [Description("Searches the history of all channels in the current server based on a regex.")]
        public async Task AllRegex(IMessage message, string regex) {
            await SearchAll(message, RegexMatch(regex));
            //TODO: Reimplement
            await message.Channel.SendMessageAsync("Unimplemented");
            //SearchAll(e => RegexMatch(e.GetArg("SearchRegex")))
        }

        [Command("ignore")]
        [Description("Mentioned channels will not be searched in ``search all``, except while in said channel. "
                            + "User must have ``Manage Channels`` permission")]
        public async Task Ignore(IMessage message, params IGuildChannel[] channels) {
            var channel = Check.InGuild(message);
            var serverConfig = Config.GetGuildConfig(channel.Guild);
            foreach (var ch in channels)
                serverConfig.GetChannelConfig(ch).Ignore();
            await message.Success();
        }

        [Command("unigore")]
        [Description("Mentioned channels will appear in ``search all`` results." 
                           +" User must have ``Manage Channels`` permission")]
        public async Task Unignore(IMessage message, params IGuildChannel[] channels) {
            var channel = Check.InGuild(message);
            var serverConfig = Config.GetGuildConfig(channel.Guild);
            foreach (var ch in channels)
                serverConfig.GetChannelConfig(ch).Unignore();
            await message.Success();
        }

        async Task SearchChannel(IMessage message, Func<string, bool> pred) {
            var channel = Check.InGuild(message);
            string reply = await ChannelSet.Get(channel).Search(pred);
            await message.Respond($"Matches found in {channel.Name}:\n{reply}");
        }

        async Task SearchAll(IMessage message, Func<string, bool> pred) {
            var builder = new StringBuilder();
            var messageChannel = Check.InGuild(message);
            var guild = messageChannel.Guild;
            var serverConfig = Config.GetGuildConfig(guild);
            var serverDirectory = ChannelLog.GuildDirectory(guild);
            foreach (string directory in Directory.GetDirectories(serverDirectory)) {
                var channelName = Path.GetFileName(directory);
                var search = ChannelLog.SearchDirectory(pred, directory);
                ulong id;
                if (ulong.TryParse(channelName, out id)) {
                    var channel = await guild.GetChannelAsync(id);
                    if (channel == null || (messageChannel != channel &&
                        serverConfig.GetChannelConfig(channel).IsIgnored))
                        continue;
                    channelName = channel.Name;
                }
                var result = await search;
                if (string.IsNullOrEmpty(result))
                    continue;
                builder.AppendLine($"Matches found in { channelName }");
                builder.AppendLine(result);
            }
            await message.Respond(builder.ToString());
        }
    }
}
