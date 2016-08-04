namespace DrumBot {
    public class UserManageRoles : ManageRoles {
        protected override bool OwnerOverride { get; } = true;
        protected override bool CheckBot { get; } = false;
    }
}