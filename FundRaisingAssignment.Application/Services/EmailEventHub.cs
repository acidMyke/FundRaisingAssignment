using FundRaisingAssignment.Application.Interfaces;
using FundRaisingAssignment.Application.Models.ProcessingModels;

namespace FundRaisingAssignment.Application.Services;

public class EmailEventHub(IServiceProvider serviceProvider)
{
    public async Task PublishAsync(EmailEvent e)
    {
        using var scope = serviceProvider.CreateScope();
        var listeners = scope.ServiceProvider.GetServices<IEmailEventListener>();

        foreach (var listener in listeners)
            await listener.OnEmailReceivedAsync(e);
    }
}