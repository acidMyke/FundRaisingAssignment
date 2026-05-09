using FundRaisingAssignment.Application.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FundRaisingAssignment.Application.Services;

public static class DonationReceiptPdf
{
    public static byte[] Generate(Donation donation)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(t => t.FontSize(11));

                page.Header()
                    .Column(col =>
                    {
                        col.Item().Text("Donation Receipt").FontSize(22).Bold();
                        col.Item().Text($"Receipt #: {donation.ReceiptNumber ?? donation.Id.ToString()}")
                            .FontColor(Colors.Grey.Darken2);
                    });

                page.Content().PaddingVertical(20).Column(col =>
                {
                    col.Spacing(10);

                    Row(col, "Date", donation.CreatedAt.ToString("f"));
                    Row(col, "Campaign", donation.Campaign?.Title ?? "—");
                    Row(col, "Amount", donation.Amount.ToString("C"));
                    Row(col, "Payment Method", donation.PaymentMethod);
                    Row(col, "Status", donation.Status.ToString());

                    if (!string.IsNullOrWhiteSpace(donation.Notes))
                        Row(col, "Notes", donation.Notes);

                    if (!string.IsNullOrWhiteSpace(donation.Message))
                        Row(col, "Message", donation.Message);
                });

                page.Footer()
                    .AlignCenter()
                    .Text(t =>
                    {
                        t.Span("Generated ").FontColor(Colors.Grey.Medium);
                        t.Span(DateTime.UtcNow.ToString("u")).FontColor(Colors.Grey.Medium);
                    });
            });
        }).GeneratePdf();
    }

    private static void Row(ColumnDescriptor col, string label, string value)
    {
        col.Item().Row(r =>
        {
            r.ConstantItem(140).Text(label).SemiBold().FontColor(Colors.Grey.Darken2);
            r.RelativeItem().Text(value);
        });
    }
}
