using System;
using Microsoft.EntityFrameworkCore;
using Remp.DataAccess.Data;
using Remp.Models.Entities;
using Remp.Repository.Interfaces;

namespace Remp.Repository.Repositories;

public class ListingCaseRepository : IListingCaseRepository
{
    private readonly RempDbContext _dbContext;

    public ListingCaseRepository(RempDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> IsActiveCompanyAdminAsync(string userId) =>
        _dbContext.PhotographyCompanies.AnyAsync(c => c.Id == userId && !c.User.IsDeleted &&
            _dbContext.UserRoles.Any(ur => ur.UserId == userId &&
                _dbContext.Roles.Any(r => r.Id == ur.RoleId && r.NormalizedName == "ADMIN")));

    public Task<Agent?> GetActiveAgentAsync(string agentId) =>
        _dbContext.Agents.AsNoTracking().Include(a => a.PhotographyCompanies).FirstOrDefaultAsync(a =>
            a.Id == agentId && !a.User.IsDeleted &&
            _dbContext.UserRoles.Any(ur => ur.UserId == a.Id &&
                _dbContext.Roles.Any(r => r.Id == ur.RoleId && r.NormalizedName == "AGENT")));

    public async Task<ListingCase> UpdateMediaSelectionAsync(int id, Func<ListingCase, Task> update)
    {
        // Validate current permissions and the entire replacement under the same transaction.
        // No tracked flag changes survive a failed validation or database write.
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var listing = await _dbContext.ListingCases.AsNoTracking()
            .Include(l => l.Agents).Include(l => l.MediaAssets)
            .SingleOrDefaultAsync(l => l.Id == id && !l.IsDeleted)
            ?? throw new KeyNotFoundException("Listing case not found.");
        await update(listing);
        var selectedIds = listing.MediaAssets.Where(m => m.IsSelected).Select(m => m.Id).ToArray();
        var heroIds = listing.MediaAssets.Where(m => m.IsHero).Select(m => m.Id).ToArray();
        var media = _dbContext.MediaAssets.Where(m => m.ListingCaseId == id);
        await media.ExecuteUpdateAsync(s => s.SetProperty(m => m.IsSelected, false).SetProperty(m => m.IsHero, false));
        await media.Where(m => selectedIds.Contains(m.Id)).ExecuteUpdateAsync(s => s.SetProperty(m => m.IsSelected, true));
        await media.Where(m => heroIds.Contains(m.Id)).ExecuteUpdateAsync(s => s.SetProperty(m => m.IsHero, true));
        await transaction.CommitAsync();
        return listing;
    }

    public async Task SetAgentAssignmentAsync(int listingCaseId, string agentId, bool assigned)
    {
        // Serialize read/modify of the existing EF skip-navigation join; no duplicate model/schema.
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var listing = await _dbContext.ListingCases.Include(x => x.Agents)
            .SingleAsync(x => x.Id == listingCaseId && !x.IsDeleted);
        var existing = listing.Agents.FirstOrDefault(a => a.Id == agentId);
        if (assigned && existing == null)
            listing.Agents.Add(await _dbContext.Agents.SingleAsync(a => a.Id == agentId));
        else if (!assigned && existing != null)
            listing.Agents.Remove(existing);
        await _dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task<bool> UserExistsAsync(string userId)
    {
        return await _dbContext.Users.AnyAsync(u => u.Id == userId);
    }

    public async Task<int> CreateListingCaseAsync(ListingCase listingCase)
    {
        await _dbContext.ListingCases.AddAsync(listingCase);
        await _dbContext.SaveChangesAsync();
        return listingCase.Id;
    }

    public async Task<ListingCase?> GetListingCaseByIdAsync(int id)
    {
        return await _dbContext.ListingCases
            .Include(x => x.Agents)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
    }

    public async Task UpdateListingCaseAsync(ListingCase listingCase)
    {
        _dbContext.ListingCases.Update(listingCase);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<int> GetListingCaseCountAsync(string userId)
    {
        return await _dbContext.ListingCases
            .Where(u => u.UserId == userId)
            .Where(x => !x.IsDeleted)
            .CountAsync();
    }
    public async Task<List<ListingCase>> GetListingCasesPagedAsync(int skip, int take, string userId)
    {
        return await _dbContext.ListingCases
            .Where(u => u.UserId == userId)
            .Where(x => !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }
    public async Task<int> GetListingCaseAssignedToAgentCountAsync(string userId)
    {
        return await _dbContext.ListingCases
            .Where(x => x.Agents.Any(a => a.Id == userId))
            .Where(x => !x.IsDeleted)
            .CountAsync();
    }
    public async Task<List<ListingCase>> GetListingCasePagedAssignedToAgentAsync(int skip, int take, string userId)
    {
        return await _dbContext.ListingCases
            .Where(x => x.Agents.Any(a => a.Id == userId))
            .Where(x => !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task DeleteListingCaseAsync(int id)
    {
        var listingCase = await _dbContext.ListingCases.FindAsync(id);
        if (listingCase != null)
        {
            _dbContext.ListingCases.Remove(listingCase);
            await _dbContext.SaveChangesAsync();
        }
    }

    public async Task<ListingCase?> GetListingCaseDetailsByIdAsync(int id)
    {
        return await _dbContext.ListingCases
            .AsNoTracking()
            .Include(x => x.Agents)
            .Include(x => x.MediaAssets.Where(m => !m.IsDeleted))
            .Include(x => x.CaseContacts)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
    }

}
