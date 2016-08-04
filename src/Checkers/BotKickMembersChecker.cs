namespace DrumBot {
    public class BotKickMembersChecker : KickMembersChecker {
        protected override bool CheckUser { get; } = false;
    }
}