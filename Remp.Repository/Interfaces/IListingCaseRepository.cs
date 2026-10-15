using System;
using Remp.Models.Entities;

namespace Remp.Repository.Interfaces;

public interface IListingCaseRepository
{
    Task<ListingCase> UpdateMediaSelectionAsync(int id, Func<ListingCase, Task> update);
    Task<bool> IsActiveCompanyAdminAsync(string userId);
    Task<Agent?> GetActiveAgentAsync(string agentId);
    Task SetAgentAssignmentAsync(int listingCaseId, string agentId, bool assigned);
    Task<bool> UserExistsAsync(string userId);
    Task<int> CreateListingCaseAsync(ListingCase listingCase);
    Task<ListingCase?> GetListingCaseByIdAsync(int id);
    Task UpdateListingCaseAsync(ListingCase listingCase);
    Task<int> GetListingCaseCountAsync(string userId);
    Task<List<ListingCase>> GetListingCasesPagedAsync(int skip, int take, string userId);
    Task<int> GetListingCaseAssignedToAgentCountAsync(string userId);
    Task<List<ListingCase>> GetListingCasePagedAssignedToAgentAsync(int skip, int take, string userId);

    Task DeleteListingCaseAsync(int id);
    Task<ListingCase?> GetListingCaseDetailsByIdAsync(int id);
}
