using Discord.Modules;
using Octokit;

namespace DrumBot.src.Modules {
    public class GithubModule : IModule {

        public GitHubClient Client { get; }

        public GithubModule() {
            Client = new GitHubClient(new ProductHeaderValue("DrumBot"));
        }

        public void Install(ModuleManager manager) {
            manager.CreateCommands("github", cbg => {
                cbg.AddCheck(Check.ManageChannels(bot:false));
                cbg.Category("Feeds");
            });
        }
    }
}
