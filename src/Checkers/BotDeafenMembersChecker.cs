namespace DrumBot {
    public class BotDeafenMembersChecker : DeafenMembersChecker {
        protected override bool CheckUser { get; } = false;
    }
}