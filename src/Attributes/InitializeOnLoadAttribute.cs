using System;

namespace DrumBot {

    /// <summary>
    /// Marks a class to be forcefully staticly initialized before the bot executes.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class InitializeOnLoadAttribute : Attribute {
        public int Order { get; set; }

        public InitializeOnLoadAttribute() : this(0) { }
        public InitializeOnLoadAttribute(int order) { Order = order; }
    }
}
