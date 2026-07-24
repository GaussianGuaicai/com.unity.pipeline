using NUnit.Framework;
using Unity.Pipeline.Runtime.Commands;

namespace Unity.Pipeline.Tests.Editor
{
#if !ENABLE_INPUT_SYSTEM
    public class RuntimeInputCommandUnavailableTests
    {
        [Test]
        public void SimulateKey_WithoutInputSystem_ReturnsUnavailableResult()
        {
            var result = RuntimeInputCommand.SimulateKey("A");

            Assert.IsFalse(result.Success);
            StringAssert.Contains("Input System", result.Error);
        }

        [Test]
        public void SimulatePointer_WithoutInputSystem_ReturnsUnavailableResult()
        {
            var result = RuntimeInputCommand.SimulatePointer(10f, 20f);

            Assert.IsFalse(result.Success);
            StringAssert.Contains("Input System", result.Error);
        }
    }
#endif
}