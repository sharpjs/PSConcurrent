using NUnit.Framework;
using NUnit.Framework.Internal;

namespace PSConcurrent
{
    internal static class TestExtensions
    {
        public static Randomizer Random
            => TestContext.CurrentContext.Random;
    }
}
