namespace DrumBot {
    public class BotManageNicknamesChecker : ManageNicknamesChecker {
        protected override bool CheckUser { get; } = false;
    }
}