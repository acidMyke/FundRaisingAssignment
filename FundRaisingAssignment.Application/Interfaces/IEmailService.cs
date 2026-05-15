using FundRaisingAssignment.Application.Models.ProcessingModels;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace FundRaisingAssignment.Application.Interfaces;

public interface IEmailService : IEmailSender
{
    Task SendEmailAsync(string email, string subject, string htmlMessage, string messageId);
}

public interface IEmailEventListener
{
    Task OnEmailReceivedAsync(EmailEvent e);
}