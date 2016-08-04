namespace DrumBot {
    public class BotManageMessagesChecker : ManageMessagesChecker {
        protected override bool CheckUser { get; } = false;
    }
}