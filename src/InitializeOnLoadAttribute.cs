using System;

namespace DrumBot {

    [AttributeUsage(AttributeTargets.Class)]
    class InitializeOnLoadAttribute : Attribute {
        public int Order { get; set; }

        public InitializeOnLoadAttribute() : this(0) { }
        public InitializeOnLoadAttribute(int order) { Order = order; }
    }
}
