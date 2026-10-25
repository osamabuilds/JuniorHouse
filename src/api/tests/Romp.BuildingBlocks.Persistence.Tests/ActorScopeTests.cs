using Romp.BuildingBlocks.Persistence.Auditing;

namespace Romp.BuildingBlocks.Persistence.Tests;

public sealed class ActorScopeTests
{
    [Fact]
    [Trait("Spec", "AC-12")]
    public void Current_OutsideAScope_IsNull()
    {
        Assert.Null(ActorScope.Current);
    }

    [Fact]
    [Trait("Spec", "AC-12")]
    public void Begin_SetsTheActor_AndDisposeRestoresThePreviousOne()
    {
        using (ActorScope.Begin("operator-cli"))
        {
            Assert.Equal("operator-cli", ActorScope.Current);

            using (ActorScope.Begin("job.sweeper"))
            {
                Assert.Equal("job.sweeper", ActorScope.Current);
            }

            Assert.Equal("operator-cli", ActorScope.Current);
        }

        Assert.Null(ActorScope.Current);
    }

    [Fact]
    [Trait("Spec", "AC-12")]
    public async Task Begin_FlowsAcrossAwait_ButNotIntoSiblingTasks()
    {
        using var scope = ActorScope.Begin("operator-cli");

        var seenAfterAwait = await Task.Run(async () =>
        {
            await Task.Yield();
            return ActorScope.Current;
        });
        Assert.Equal("operator-cli", seenAfterAwait);

        string? seenInOtherFlow = "unset";
        using (var gate = new ManualResetEventSlim())
        {
            var thread = new Thread(() =>
            {
                seenInOtherFlow = ActorScope.Current;
                gate.Set();
            });
            using (ExecutionContext.SuppressFlow())
            {
                thread.Start();
            }
            gate.Wait();
        }

        Assert.Null(seenInOtherFlow);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Upper")]
    [InlineData("has space")]
    [InlineData("line\nbreak")]
    [InlineData("semi;colon")]
    public void Begin_RejectsNamesOutsideTheActorCharset(string name)
    {
        Assert.Throws<ArgumentException>(() => ActorScope.Begin(name));
    }

    [Theory]
    [InlineData("system", true)]
    [InlineData("operator-cli", true)]
    [InlineData("jane.doe_1", true)]
    [InlineData("svc:billing", true)]
    [InlineData("Jane", false)]
    [InlineData("a b", false)]
    [InlineData("a\r\nb", false)]
    [InlineData("", false)]
    public void IsValid_FollowsTheAuditCharset(string name, bool expected)
    {
        Assert.Equal(expected, ActorScope.IsValid(name));
    }
}
