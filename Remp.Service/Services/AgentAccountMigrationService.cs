using Remp.Models.Entities;
using Remp.Repository.Interfaces;

namespace Remp.Service.Services;

// Invoked explicitly by the maintenance command, never from HTTP requests or startup seeding.
public sealed class AgentAccountMigrationService(IAgentAccountMigrationRepository repository)
{
    public Task<int> MigrateAsync(IReadOnlyList<AgentAccountMapping> mappings, bool apply = false)
    {
        if (mappings == null || mappings.Count == 0)
            throw new ArgumentException("At least one explicit account mapping is required.");
        if (mappings.Any(m => m == null || string.IsNullOrWhiteSpace(m.UserId) || string.IsNullOrWhiteSpace(m.CompanyId) ||
            string.IsNullOrWhiteSpace(m.FirstName) || string.IsNullOrWhiteSpace(m.LastName) ||
            m.FirstName.Length > 100 || m.LastName.Length > 100 || m.AvatarUrl?.Length > 1000 ||
            (!string.IsNullOrWhiteSpace(m.AvatarUrl) && !Uri.TryCreate(m.AvatarUrl, UriKind.Absolute, out _))))
            throw new ArgumentException("Each mapping requires user ID, company ID, valid names and an optional absolute avatar URL.");
        if (mappings.Select(m => m.UserId).Distinct().Count() != mappings.Count)
            throw new ArgumentException("Duplicate mapped user IDs are not allowed.");
        return repository.MigrateAsync(mappings, apply);
    }
}
