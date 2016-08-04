namespace DrumBot {
    public class UserManageMessagesChecker : ManageMessagesChecker {
        protected override bool OwnerOverride { get; } = true;
        protected override bool CheckBot { get; } = false;
    }
}