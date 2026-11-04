namespace PirryLedger.Core.AccessControl.Api;

public sealed record BranchRequest(string Name, string Address);

public sealed record UpdateBranchRequest(string Name, string Address, bool Active);

public sealed record AssignUserBranchRequest(Guid BranchId);
