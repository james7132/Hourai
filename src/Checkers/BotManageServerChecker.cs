namespace DrumBot {
    public class BotManageServerChecker : ManageServerChecker {
        protected override bool CheckUser { get; } = false;
    }
}