using FundRaisingAssignment.Application.Models.ProcessingModels;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace FundRaisingAssignment.Application.Interfaces;

public interface IEmailService : IEmailSender
{
}

public interface IEmailEventListener
{
    Task OnEmailReceivedAsync(EmailEvent e);
}