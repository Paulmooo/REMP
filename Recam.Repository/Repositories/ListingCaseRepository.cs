using System;
using Microsoft.EntityFrameworkCore;
using Recam.DataAccess.Data;
using Recam.Models.Entities;
using Recam.Repository.Interfaces;

namespace Recam.Repository.Repositories;

public class ListingCaseRepository : IListingCaseRepository
{
    private readonly RecamDbContext _dbContext;

    public ListingCaseRepository(RecamDbContext dbContext)
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
        return await _dbContext.ListingCases.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
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
}
