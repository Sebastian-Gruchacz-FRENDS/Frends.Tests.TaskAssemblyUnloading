namespace UnloadTests.Targets
{
    public static class SimpleTestTarget
    {
        public static void NoArgs() { }

        public static void OneArg(int x) { }

        public static void OneArg(string s) { }

        public static void TwoArgs(int x, string y) { }

        public static void Defaults(int x = 5, string y = "ok") { }

        public static void Throwing(int x)
        {
            throw new InvalidOperationException("Boom");
        }
    }
}
