using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace DrumBot {

    [Module("search", AutoLoad = false)]
    [PublicOnly]
    [ModuleCheck]
    public class Search {

        public static ChannelSet ChannelSet { get; private set; }

        public Search(ChannelSet channels) {
            ChannelSet = Check.NotNull(channels);
        }

        static Func<string, bool> ExactMatch(IEnumerable<string> matches) {
            return s => matches.All(s.Contains);
        }

        static Func<string, bool> RegexMatch(string regex) {
            return new Regex(regex, RegexOptions.Compiled).IsMatch;
        }

        [Command]
        [Remarks("Search the history of the current channel for messages that match all of the specfied search terms.")]
        public async Task SearchChat(IUserMessage message, params string[] terms) {
            await SearchChannel(message, ExactMatch(terms));
        }

        [Command("regex")]
        [Remarks("Search the history of the current channel for matches to a specfied regex.")]
        public async Task SearchRegex(IUserMessage message, string regex) {
            await SearchChannel(message, RegexMatch(regex));
        }

        [Command("day")]
        [Remarks("SearchChat the log of the the current channel on a certain day. Day must be of the format ``yyyy-mm-dd``")]
        public async Task Day(IUserMessage message, string day) {
            var channel = Check.InGuild(message);
            string path = ChannelSet.Get(channel).GetPath(day);
            if (File.Exists(path))
                await message.SendFileRetry(path);
            else
                await message.Respond($"A log for {channel.Mention} on date {day} cannot be found.");
        }

        [Command("ignore")]
        [Remarks("Mentioned channels will not be searched in ``search all``, except while in said channel. "
                            + "User must have ``Manage Channels`` permission")]
        public async Task Ignore(IUserMessage message, params IGuildChannel[] channels) {
            var channel = Check.InGuild(message);
            var serverConfig = Config.GetGuildConfig(channel.Guild);
            foreach (var ch in channels)
                serverConfig.GetChannelConfig(ch).Ignore();
            await message.Success();
        }

        [Command("unigore")]
        [Remarks("Mentioned channels will appear in ``search all`` results." 
                           +" User must have ``Manage Channels`` permission")]
        public async Task Unignore(IUserMessage message, params IGuildChannel[] channels) {
            var channel = Check.InGuild(message);
            var serverConfig = Config.GetGuildConfig(channel.Guild);
            foreach (var ch in channels)
                serverConfig.GetChannelConfig(ch).Unignore();
            await message.Success();
        }

        [Group("all")]
        public class All {

            [Command]
            [Remarks("Searches the history of all channels in the current server for any of the specfied search terms.")]
            public async Task SearchAll(IUserMessage message, params string[] terms) {
                await SearchAll(message, ExactMatch(terms));
            }

            [Command("regex")]
            [Remarks("Searches the history of all channels in the current server based on a regex.")]
            public async Task SearchAllRegex(IUserMessage message, string regex) {
                await SearchAll(message, RegexMatch(regex));
            }

            async Task SearchAll(IUserMessage message, Func<string, bool> pred) {
                try {
                    var channel = Check.InGuild(message);
                    string reply = await ChannelSet.Get(channel).SearchAll(pred);
                    await message.Respond(reply);
                } catch (Exception e) {
                    Log.Error(e);
                }
            }
        }

        async Task SearchChannel(IUserMessage message, Func<string, bool> pred) {
            var channel = Check.InGuild(message);
            string reply = await ChannelSet.Get(channel).Search(pred);
            await message.Respond(reply);
            //await message.Respond($"Matches found in {channel.Name}:\n{reply}");
        }

    }
}
