namespace Remp.Models.Entities;

// Explicit operator-supplied mapping; never inferred from existing account names.
public sealed record AgentAccountMapping(string UserId, string CompanyId, string FirstName, string LastName, string? AvatarUrl = null);
