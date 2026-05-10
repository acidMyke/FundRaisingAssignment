using FundRaisingAssignment.Application.Data;
using FundRaisingAssignment.Application.Interfaces.Repositories;
using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Models.ProcessingModels;
using Microsoft.EntityFrameworkCore;

namespace FundRaisingAssignment.Application.Repositories;

public class CampaignDigestRepository(ApplicationDbContext dbContext) : ICampaignDigestRepository
{

    public Task SaveChangesAsync()
    {
        return dbContext.SaveChangesAsync();
    }
}
