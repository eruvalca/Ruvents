namespace Ruvents.SharedKernel.Planning;

public sealed record PlanningPotluckSlot(
    Guid Id,
    string Name,
    string? Description,
    int QuantityNeeded,
    int QuantityClaimed,
    bool IsCustom,
    IReadOnlyList<PlanningPotluckClaim> Claims);
