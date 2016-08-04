namespace DrumBot {
    public class UserMuteMembersChecker : MuteMembersChecker {
        protected override bool OwnerOverride { get; } = true;
        protected override bool CheckBot { get; } = false;
    }
}