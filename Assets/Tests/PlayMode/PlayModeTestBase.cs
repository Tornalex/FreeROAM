using System.Collections;
using UnityEngine.TestTools;

namespace SpearWander.PlayModeTests
{
    /// <summary>
    /// Base class for PlayMode tests that need fixture-managed isolation.
    /// UnityTearDown runs even when a test fails, so fixture cleanup is not
    /// skipped by an assertion failure.
    /// </summary>
    public abstract class PlayModeTestBase
    {
        protected PlayModeTestFixture Fixture { get; private set; }

        [UnitySetUp]
        public IEnumerator SetUpFixture()
        {
            Fixture = new PlayModeTestFixture();
            Fixture.SetUp();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDownFixture()
        {
            if (Fixture == null)
                yield break;

            yield return Fixture.Cleanup();
        }
    }
}
