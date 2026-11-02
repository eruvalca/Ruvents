namespace Ruvents.SharedKernel.Planning;

public sealed record PlanningPotluckClaim(string UserId, string DisplayName, int Quantity, string? DishNote, bool IsMine);
