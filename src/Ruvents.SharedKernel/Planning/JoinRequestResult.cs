namespace Ruvents.SharedKernel.Planning;

public enum JoinRequestResult
{
    Requested = 0,
    AlreadyMember = 1,
    AlreadyRequested = 2,
    GroupNotFound = 3,
}
