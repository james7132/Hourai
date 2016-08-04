namespace DrumBot {
    public class BotMuteMembersChecker : MuteMembersChecker {
        protected override bool CheckUser { get; } = false;
    }
}