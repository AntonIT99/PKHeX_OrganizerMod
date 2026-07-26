using OrganizerMod.Domain;

namespace OrganizerMod.Tests;

public sealed class OrganizationPlanTests
{
    [Fact]
    public void EmptyPlanHasNoMoves()
    {
        Assert.Empty(OrganizationPlan.Empty.Moves);
    }

    [Fact]
    public void PlanReportsExpectedMoveCount()
    {
        var plan = new OrganizationPlan(
        [
            new SlotMove(new SlotPosition(0, 0), new SlotPosition(1, 0)),
            new SlotMove(new SlotPosition(0, 1), new SlotPosition(1, 1)),
        ]);

        Assert.Equal(2, plan.Moves.Count);
    }

    [Fact]
    public void MovePreservesSourceAndDestination()
    {
        var source = new SlotPosition(2, 4);
        var destination = new SlotPosition(5, 8);

        var move = new SlotMove(source, destination);

        Assert.Equal(source, move.Source);
        Assert.Equal(destination, move.Destination);
    }

    [Fact]
    public void NegativePositionIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SlotPosition(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SlotPosition(0, -1));
    }

    [Fact]
    public void MoveToSamePositionIsRejected()
    {
        var position = new SlotPosition(1, 2);

        Assert.Throws<ArgumentException>(() => new SlotMove(position, position));
    }

    [Fact]
    public void SampleRuleProducesDeterministicPlan()
    {
        var rule = new OrganizationRule(2, 5);

        var first = rule.CreateSamplePlan();
        var second = rule.CreateSamplePlan();

        Assert.Equal(first.Moves, second.Moves);
        var move = Assert.Single(first.Moves);
        Assert.Equal(new SlotPosition(2, 0), move.Source);
        Assert.Equal(new SlotPosition(5, 0), move.Destination);
    }
}
