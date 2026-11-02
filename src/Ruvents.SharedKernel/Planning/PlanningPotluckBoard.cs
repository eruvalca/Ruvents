namespace Ruvents.SharedKernel.Planning;

public sealed record PlanningPotluckBoard(
    bool Enabled,
    bool CanManage,
    bool CanClaim,
    IReadOnlyList<PlanningPotluckSlot> Slots);
