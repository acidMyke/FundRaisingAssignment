using FundRaisingAssignment.Application.Interfaces;
using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Models.ProcessingModels;
using Microsoft.AspNetCore.Identity;

namespace FundRaisingAssignment.Application.Services;

public class UserEmailBounceListener(UserManager<ApplicationUser> userManager, ILogger<UserEmailBounceListener> logger) : IEmailEventListener
{
    public async Task OnEmailReceivedAsync(EmailEvent e)
    {
        if (e.Status != EmailStatus.Bounced && e.Status != EmailStatus.Spam) return;
        var user = await userManager.FindByEmailAsync(e.Email);
        if (user == null) return;
        if (user.IsEmailBounced) return;

        user.IsEmailBounced = true;
        var result = await userManager.UpdateAsync(user);
        if (result.Succeeded)
            logger.LogInformation("User {Email} marked as bounced due to status {Status}", e.Email, e.Status);
        else
            logger.LogError("Failed to update user {Email} bounce status: {Errors}", e.Email, string.Join(", ", result.Errors.Select(er => er.Description)));
    }
}
