using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace RiseFlow.Api.Services;

/// <summary>
/// Generates the Standard Nigerian Grading Reference and RiseFlow Support Promise as a downloadable PDF.
/// </summary>
public class GradingReferencePdfService
{
    private const string PrimaryColor = "#1E40AF";
    private const string AccentColor = "#059669";

    public byte[] GeneratePdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.PageColor(Colors.White);

                page.Header().Column(c =>
                {
                    c.Item().Text("RiseFlow").Bold().FontSize(20).FontColor(PrimaryColor);
                    c.Item().Text("Standard Nigerian Grading Reference (Internal Use)").FontSize(14).FontColor(Colors.Grey.Darken2);
                });

                page.Content().Column(c =>
                {
                    c.Spacing(12);
                    c.Item().Padding(10).Background(Colors.Grey.Lighten4).Text("RiseFlow defaults to this standard, but can be changed by your Admin.").FontSize(10).Italic().FontColor(Colors.Grey.Darken1);
                    c.Spacing(20);

                    // Grading table
                    c.Item().Text("Score range & grade definitions").Bold().FontSize(12).FontColor(AccentColor);
                    c.Spacing(8);
                    c.Item().Table(t =>
                    {
                        t.ColumnsDefinition(d =>
                        {
                            d.ConstantColumn(100);
                            d.ConstantColumn(70);
                            d.RelativeColumn(3);
                        });
                        t.Header(h =>
                        {
                            h.Cell().Element(HeaderStyle).Text("Score Range");
                            h.Cell().Element(HeaderStyle).Text("Grade");
                            h.Cell().Element(HeaderStyle).Text("Definition");
                        });
                        AddRow(t, "75 - 100", "A1", "Excellent");
                        AddRow(t, "70 - 74", "B2", "Very Good");
                        AddRow(t, "65 - 69", "B3", "Good");
                        AddRow(t, "50 - 64", "C4 - C6", "Credit (Passing Grade)");
                        AddRow(t, "40 - 49", "D7 - E8", "Pass");
                        AddRow(t, "0 - 39", "F9", "Fail");
                    });

                    c.Spacing(32);
                    c.Item().PaddingTop(16).Border(1).BorderColor(Colors.Grey.Lighten1).Padding(16).Column(support =>
                    {
                        support.Item().Text("The RiseFlow Support Promise").Bold().FontSize(14).FontColor(PrimaryColor);
                        support.Spacing(8);
                        support.Item().Text("If you get stuck, simply click the \"Help\" icon at the bottom of your dashboard to chat with our 24/7 support team.").FontSize(11);
                    });
                });

                page.Footer().AlignCenter().Text("RiseFlow — riseflow.com").FontSize(8).FontColor(Colors.Grey.Medium);
            });
        }).GeneratePdf();
    }

    private static IContainer HeaderStyle(IContainer container) =>
        container.DefaultTextStyle(x => x.SemiBold()).Background(Colors.Grey.Lighten3).Padding(8);

    private static void AddRow(TableDescriptor t, string range, string grade, string definition)
    {
        t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(6).Text(range).FontSize(10);
        t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(6).Text(grade).FontSize(10).SemiBold();
        t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(6).Text(definition).FontSize(10);
    }
}
