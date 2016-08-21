using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Commands.Permissions.Visibility;

namespace DrumBot {
    public class BotOwnerCommandService : IService {
        public void Install(DiscordClient client) {
            var commandService = client.GetService<CommandService>();
            commandService.CreateGroup("", cbg => {
                cbg.AddCheck(new BotOwnerChecker());
                cbg.CreateCommand("getbotlog")
                    .PrivateOnly()
                    .Do(async e => await e.User.SendFileRetry(Bot.BotLog));
                cbg.CreateCommand("broadcast")
                    .PrivateOnly()
                    .Parameter("Message", ParameterType.Unparsed)
                    .Do(async e => {
                        await Task.WhenAll(from server in e.Message.Client.Servers
                                           select server.DefaultChannel.SendMessage(e.GetArg("Message")));
                    });
                cbg.CreateCommand("id")
                   .Parameter("Targets", ParameterType.Multiple)
                   .Do(async e => {
                       var response = new StringBuilder();
                       var message = e.Message;
                       if(message.MentionedUsers.Any())
                           response.AppendLine(message.MentionedUsers.Select(u => $"{u.Name}: {u.Id}").Join("\n"));
                       if(message.MentionedChannels.Any())
                           response.AppendLine(message.MentionedChannels.Select(c => $"{c.Name}: {c.Id}").Join("\n"));
                       if(message.MentionedRoles.Any())
                           response.AppendLine(message.MentionedRoles.Select(r => $"{r.Name}: {r.Id}").Join("\n"));
                       await e.Respond(response.ToString());
                   });
            });
        }
    }
}
