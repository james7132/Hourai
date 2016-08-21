using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.Commands.Permissions.Visibility;
using Discord.Modules;
using static System.String;

namespace DrumBot {
    public class InfoModule : IModule {

        public void Install(ModuleManager manager) {
            manager.CreateCommands("", cbg => {
                cbg.PublicOnly();
                cbg.CreateCommand("avatar")
                    .Description("Gets the avatar url of all mentioned users.")
                    .Parameter("User(s)", ParameterType.Multiple)
                    .Do(Avatar);

                cbg.CreateCommand("whois")
                    .Description("Gets information on a specified user")
                    .Parameter("User", ParameterType.Optional)
                    .Do(Command.Response(e => WhoIs(e)));
                
                cbg.CreateCommand("serverinfo")
                   .Description("Gets general information about the current server")
                   .Do(Command.Response(e => Info(e)));
            });
        }

        StringBuilder Info(CommandEventArgs e) {
            var builder = new StringBuilder();
            var server = e.Server;
            var config = Config.GetServerConfig(e.Server);
            builder.AppendLine($"Name: {server.Name.Code()}");
            builder.AppendLine($"ID: {server.Id.ToString().Code()}");
            builder.AppendLine($"Owner: {server.Owner.Name.Code()}");
            builder.AppendLine($"Region: {server.Region.Name.Code()}");
            builder.AppendLine($"User Count: {server.UserCount.ToString().Code()}");
            builder.AppendLine($"Roles: {server.Roles.Order().Select(r => r.Name.Code()).Join(", ")}");
            builder.AppendLine($"Text Channels: {server.TextChannels.Order().Select(ch => ch.Name.Code()).Join(", ")}");
            builder.AppendLine($"Voice Channels: {server.VoiceChannels.Order().Select(ch => ch.Name.Code()).Join(", ")}");
            builder.AppendLine($"Server Type: {config.Type.ToString().Code()}");
            if(!IsNullOrEmpty(server.IconUrl))
                builder.AppendLine(server.IconUrl);
            return builder;
        }

        static async Task Avatar(CommandEventArgs e) {
            if(!e.Message.MentionedUsers.Any()) {
                await e.Respond(e.User.AvatarUrl);
            } else {
                await Command.ForEvery(e, e.Message.MentionedUsers, user => $"{user.Name}: {user.AvatarUrl}"); 
            }
        }

        string ProcessDate(DateTime? time) {
            if (time == null)
                return "N/A".Code();
            var dtString = time.ToString();
            var diff = DateTime.UtcNow - time.Value;
            dtString += $" UTC ({GetReadableTimespan(diff)} ago)";
            return dtString.Code();
        }

        StringBuilder WhoIs(CommandEventArgs e) {
            var targetUser = e.Message.MentionedUsers.FirstOrDefault();
            if (targetUser == null)
                targetUser = e.User;
            var builder = new StringBuilder();
            builder.AppendLine($"{e.User.Mention}");
            var userNameData = targetUser.Name;
            if (targetUser.IsBot)
                userNameData += "(BOT)";
            if (targetUser.IsServerOwner())
                userNameData += " (Server Owner)";
            if (targetUser.IsBotOwner())
                userNameData += " (Bot Owner)";
            builder.AppendLine($"Username: {userNameData.Code()}");
            builder.AppendLine($"Nickname: {(targetUser.Nickname.NullIfEmpty() ?? "N/A").Code()}");
            builder.AppendLine($"Current Game: {(targetUser.CurrentGame?.Name ?? "N/A").Code()}");
            builder.AppendLine($"ID: {targetUser.Id.ToString().Code()}");
            builder.AppendLine($"Joined on: {ProcessDate(targetUser.JoinedAt)}");
            builder.AppendLine($"Created on: {ProcessDate(targetUser.CreatedOn())}");
            builder.AppendLine($"Last Activity: {ProcessDate(targetUser.LastActivityAt)}");
            builder.AppendLine($"Last Online: {ProcessDate(targetUser.LastOnlineAt)}");
            if(targetUser.Roles.Any(r => !r.IsEveryone))
                builder.AppendLine($"Roles: {targetUser.Roles.Where(r => !r.IsEveryone).Select(r => r.Name.Code()).Join(", ")}");
            if(!IsNullOrEmpty(targetUser.AvatarUrl))
                builder.AppendLine(targetUser.AvatarUrl);
            return builder;
        }
        public string GetReadableTimespan(TimeSpan ts) {
            // formats and its cutoffs based on totalseconds
            var cutoff = new SortedList<long, string> {
               {60, "{3:S}" },
               {60*60, "{2:M}, {3:S}"},
               {24*60*60, "{1:H}, {2:M}"},
               {long.MaxValue , "{0:D}, {1:H}"}
             };

            // find nearest best match
            var find = cutoff.Keys.ToList()
                          .BinarySearch((long) ts.TotalSeconds);
            // negative values indicate a nearest match
            var near = find < 0 ? Math.Abs(find) - 1 : find;
            // use custom formatter to get the string
            return Format(
                new HMSFormatter(),
                cutoff[cutoff.Keys[near]],
                ts.Days,
                ts.Hours,
                ts.Minutes,
                ts.Seconds);
        }

        // formatter for forms of
        // seconds/hours/day
        public class HMSFormatter : ICustomFormatter, IFormatProvider {
            // list of Formats, with a P customformat for pluralization
            static readonly Dictionary<string, string> Timeformats = new Dictionary<string, string> {
                {"S", "{0:P:seconds:second}"},
                {"M", "{0:P:minutes:minute}"},
                {"H","{0:P:hours:hour}"},
                {"D", "{0:P:days:day}"},
            };

            public string Format(string format, object arg, IFormatProvider formatProvider) {
                return string.Format(new PluralFormatter(), Timeformats[format], arg);
            }

            public object GetFormat(Type formatType) {
                return formatType == typeof(ICustomFormatter) ? this : null;
            }
        }

        // formats a numeric value based on a format P:Plural:Singular
        public class PluralFormatter : ICustomFormatter, IFormatProvider {

            public string Format(string format, object arg, IFormatProvider formatProvider) {
                if (arg == null)
                    return String.Format(format, arg);
                var parts = format.Split(':'); // ["P", "Plural", "Singular"]

                if (parts[0] != "P")
                    return String.Format(format, arg);
                // which index postion to use
                int partIndex = (arg.ToString() == "1") ? 2 : 1;
                // pick string (safe guard for array bounds) and format
                return $"{arg} {(parts.Length > partIndex ? parts[partIndex] : "")}";
            }

            public object GetFormat(Type formatType) {
                return formatType == typeof(ICustomFormatter) ? this : null;
            }
        }
    }
}
