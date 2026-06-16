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
