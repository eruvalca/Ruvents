namespace Ruvents.SharedKernel.Events;

/// <summary>
/// Where an event sits in time for the person viewing it. Cancellation is a separate
/// concern carried by <see cref="EventStatus"/>.
/// </summary>
public enum EventTimeState
{
    Upcoming,
    InProgress,
    Past,
}
