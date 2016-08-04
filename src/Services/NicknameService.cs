using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace DrumBot.src.Services {
    public class NicknameService : IService{

        public Config Config { get; }

        public NicknameService(Config config) {
            Config = Check.NotNull(config);
        }

        public void Install(DiscordClient client) {
            var commandService = client.GetService<CommandService>();
            commandService.CreateGroup("nickname",
                cbg => {
                    cbg.CreateCommand("set")
                       .Description("Sets the nickname of all mentioned users. Requires ``Manage Nickame`` permission on both user and bot.")
                       .Parameter("Nickname")
                       .Parameter("Users", ParameterType.Multiple)
                       .AddCheck(new ProdChecker())
                       .AddCheck(Check.ManageNicknames())
                       .Do(SetNickname);
                });
        }

        async Task SetNickname(CommandEventArgs e) {
            await Command.ForEvery(e, e.Message.MentionedUsers, 
                Command.Action(e.Channel, "change nickname", async user => await user.SetNickname(e.GetArg("Nickname"))));
        }
    }
}
