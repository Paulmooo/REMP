using Remp.Models.Entities;

namespace Remp.Repository.Interfaces;

public interface IAgentAccountMigrationRepository
{
    Task<int> MigrateAsync(IReadOnlyList<AgentAccountMapping> mappings, bool apply);
}
