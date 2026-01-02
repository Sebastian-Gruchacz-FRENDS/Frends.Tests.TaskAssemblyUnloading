using Frends.Tests.TaskAssemblyUnloading;
using NUnit.Framework;

namespace UnloadingTestsAndSamples
{
    [TestFixture]
    public class Samples
    {
        [Test]
        public void LongSyntax()
        {
            UnloadTest
                .From("MyPlugin.dll")
                .Type("My.Namespace.Worker")
                .TaskMethod("Run")
                .WithArgs(42, "abc")
                .Execute();
        }

        [Test]
        public void LongSyntaxNoParams()
        {
            UnloadTest
                .From("MyPlugin.dll")
                .Type("My.Namespace.Worker")
                .TaskMethod("Run")
                .Execute();
        }

        [Test]
        public void ShortSyntax()
        {
            UnloadTest
                .Invoke("MyPlugin.dll", "My.Namespace.Worker", "Run", 42, "abc")
                .Execute();
        }

        [Test]
        public void ShortSyntaxNoParams()
        {
            UnloadTest
                .Invoke("MyPlugin.dll", "My.Namespace.Worker", "Run")
                .Execute();
        }
    }
}
