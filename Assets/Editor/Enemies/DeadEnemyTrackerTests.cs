using NUnit.Framework;
using SpearWander.Abilities;

[TestFixture]
public class DeadEnemyTrackerTests
{
    [SetUp]
    public void SetUp()
    {
        DeadEnemyTracker.ClearAll();
    }

    [TearDown]
    public void TearDown()
    {
        DeadEnemyTracker.ClearAll();
    }

    [Test]
    public void MarkDeadMakesIsDeadTrueForSameRoomAndIdentity()
    {
        DeadEnemyTracker.MarkDead("room-alpha", "enemy-1");

        Assert.IsTrue(DeadEnemyTracker.IsDead("room-alpha", "enemy-1"));
    }

    [Test]
    public void SameIdentityInDifferentRoomIsNotDead()
    {
        DeadEnemyTracker.MarkDead("room-alpha", "enemy-1");

        Assert.IsTrue(DeadEnemyTracker.IsDead("room-alpha", "enemy-1"));
        Assert.IsFalse(DeadEnemyTracker.IsDead("room-beta", "enemy-1"));
    }

    [Test]
    public void RepeatedMarkDeadIsIdempotent()
    {
        DeadEnemyTracker.MarkDead("room-alpha", "enemy-1");
        DeadEnemyTracker.MarkDead("room-alpha", "enemy-1");
        DeadEnemyTracker.MarkDead("room-alpha", "enemy-1");

        Assert.IsTrue(DeadEnemyTracker.IsDead("room-alpha", "enemy-1"));
        Assert.IsFalse(DeadEnemyTracker.IsDead("room-beta", "enemy-1"));
    }

    [Test]
    public void ClearRoomOnlyAffectsSpecifiedKey()
    {
        DeadEnemyTracker.MarkDead("room-alpha", "enemy-1");
        DeadEnemyTracker.MarkDead("room-beta", "enemy-2");

        DeadEnemyTracker.ClearRoom("room-alpha");

        Assert.IsFalse(DeadEnemyTracker.IsDead("room-alpha", "enemy-1"));
        Assert.IsTrue(DeadEnemyTracker.IsDead("room-beta", "enemy-2"));
    }

    [Test]
    public void ClearAllClearsEveryRoomKey()
    {
        DeadEnemyTracker.MarkDead("room-alpha", "enemy-1");
        DeadEnemyTracker.MarkDead("room-beta", "enemy-2");

        DeadEnemyTracker.ClearAll();

        Assert.IsFalse(DeadEnemyTracker.IsDead("room-alpha", "enemy-1"));
        Assert.IsFalse(DeadEnemyTracker.IsDead("room-beta", "enemy-2"));
    }
}
