using Frends.Tests.TaskAssemblyUnloading;
using NUnit.Framework;

namespace UnloadTests.Tests
{
    [TestFixture]
    [Ignore("Just RUN Samples, there is no existing library for them.")]
    public class Samples
    {
        [Test]
        public void LongSyntaxWithSystemParams()
        {
            UnloadTest
                .From("MyPlugin.dll")
                .Type("My.Namespace.Worker")
                .TaskMethod("Run")
                .WithArgs(false, 42, "abc")
                .Execute();
        }

        [Test]
        public void LongSyntaxNoParams()
        {
            UnloadTest
                .From("MyPlugin.dll")
                .Type("My.Namespace.Worker")
                .TaskMethod("Run")
                .ExecuteWithoutSerialization();
        }

        [Test]
        public void ShortSyntax()
        {
            UnloadTest
                .Invoke("MyPlugin.dll", "My.Namespace.Worker", "Run", 42, "abc")
                .Execute();
        }

        [Test]
        public void ShortSyntaxWithDefaultParams()
        {
            UnloadTest
                .Invoke("MyPlugin.dll", "My.Namespace.Worker", "Run")
                .Execute();
        }

        [Test]
        public void ShortSyntaxWithEmbeddedParams()
        {
            UnloadTest
                .Invoke("MyPlugin.dll", "My.Namespace.TaskWorker", "RunWithOptions", null) // new My.Namespace.TaskOptions()
                .Execute();
        }
    }
}
