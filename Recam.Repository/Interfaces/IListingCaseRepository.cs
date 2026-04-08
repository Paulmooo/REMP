using System;
using Recam.Models.Entities;

namespace Recam.Repository.Interfaces;

public interface IListingCaseRepository
{
    Task<bool> UserExistsAsync(string userId);
    Task<int> CreateListingCaseAsync(ListingCase listingCase);
    Task<ListingCase?> GetListingCaseByIdAsync(int id);
    Task UpdateListingCaseAsync(ListingCase listingCase);

}
