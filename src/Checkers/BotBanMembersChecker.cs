namespace DrumBot {
    public class BotBanMembersChecker : BanMembersChecker {
        protected override bool CheckUser { get; } = false;
    }
}