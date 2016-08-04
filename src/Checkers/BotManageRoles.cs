namespace DrumBot {
    public class BotManageRoles : ManageRoles {
        protected override bool CheckUser { get; } = false;
    }
}