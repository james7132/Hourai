namespace DrumBot {
    public class UserManageServerChecker : ManageServerChecker {
        protected override bool OwnerOverride { get; } = true;
        protected override bool CheckBot { get; } = false;
    }
}