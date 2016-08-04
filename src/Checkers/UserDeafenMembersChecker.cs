namespace DrumBot {
    public class UserDeafenMembersChecker : DeafenMembersChecker {
        protected override bool OwnerOverride { get; } = true;
        protected override bool CheckBot { get; } = false;
    }
}