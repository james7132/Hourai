namespace DrumBot {
    public class UserManageNicknamesChecker : ManageNicknamesChecker {
        protected override bool OwnerOverride { get; } = true;
        protected override bool CheckBot { get; } = false;
    }
}