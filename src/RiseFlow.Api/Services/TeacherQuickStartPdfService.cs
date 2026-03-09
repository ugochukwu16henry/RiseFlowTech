using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace RiseFlow.Api.Services;

/// <summary>
/// Generates the RiseFlow Teacher's Quick Start Guide as a downloadable PDF.
/// </summary>
public class TeacherQuickStartPdfService
{
    private const string PrimaryColor = "#1E40AF";
    private const string AccentColor = "#059669";

    public byte[] GeneratePdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(container =>
        {
            // Cover
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.PageColor(Colors.White);
                page.Content().Column(c =>
                {
                    c.Spacing(24);
                    c.Item().AlignCenter().Text("RiseFlow").Bold().FontSize(32).FontColor(PrimaryColor);
                    c.Item().AlignCenter().Text("Teacher's Quick Start Guide").FontSize(20).FontColor(Colors.Grey.Darken2);
                    c.Spacing(48);
                    c.Item().Padding(16).Background(Colors.Grey.Lighten3).Column(goal =>
                    {
                        goal.Spacing(8);
                        goal.Item().Text("Goal").Bold().FontSize(12).FontColor(AccentColor);
                        goal.Item().Text("Move from \"Paper Records\" to \"Digital Excellence\" in 3 easy steps.").FontSize(12);
                    });
                    c.Spacing(80);
                    c.Item().AlignCenter().Text("Secondary School → Scoring  |  Primary School → Assessments").FontSize(10).FontColor(Colors.Grey.Medium);
                });
                page.Footer().AlignCenter().Text("riseflow.com").FontSize(9).FontColor(Colors.Grey.Medium);
            });

            // Step 1: Secondary School Result Entry
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.Header().Row(r =>
                {
                    r.ConstantItem(36).Height(36).Width(36).Background(PrimaryColor).AlignCenter().AlignMiddle().Text("1").Bold().FontSize(18).FontColor(Colors.White);
                    r.RelativeItem().PaddingLeft(12).AlignMiddle().Text("The Secondary School Result Entry").Bold().FontSize(16).FontColor(PrimaryColor);
                });
                page.Content().Column(c =>
                {
                    c.Spacing(8);
                    c.Item().Text("For Secondary schools, RiseFlow uses the standard Nigerian 70/30 or 60/40 split.").FontSize(11).FontColor(Colors.Grey.Darken1);
                    c.Spacing(16);

                    AddBullet(c, "Select Your Class", "Log in and select the class (e.g., JSS 1 Gold) and your Subject (Mathematics).");
                    AddBullet(c, "Enter Scores", "You will see a grid of all students.");
                    AddBullet(c, "CA (30%)", "Enter the sum of tests and assignments.");
                    AddBullet(c, "Exam (70%)", "Enter the final exam score.");
                    AddBullet(c, "Auto-Calculation", "Notice that as you type, RiseFlow automatically calculates the Total, Grade (A1, B2, etc.), and Remarks.");
                    AddBullet(c, "Save & Sync", "Click \"Save Draft\" to continue later or \"Submit to Principal\" for final approval.");
                });
                page.Footer().AlignCenter().Text("RiseFlow — Teacher's Quick Start Guide").FontSize(8).FontColor(Colors.Grey.Medium);
            });

            // Step 2: Primary School Assessment
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.Header().Row(r =>
                {
                    r.ConstantItem(36).Height(36).Width(36).Background(AccentColor).AlignCenter().AlignMiddle().Text("2").Bold().FontSize(18).FontColor(Colors.White);
                    r.RelativeItem().PaddingLeft(12).AlignMiddle().Text("The Primary School Assessment (Customized)").Bold().FontSize(16).FontColor(AccentColor);
                });
                page.Content().Column(c =>
                {
                    c.Spacing(8);
                    c.Item().Text("For Primary schools, we focus on behavioral and psychomotor development.").FontSize(11).FontColor(Colors.Grey.Darken1);
                    c.Spacing(16);

                    AddBullet(c, "Define Assessment", "If your school uses unique criteria (e.g., \"Neatness of Uniform\" or \"Reading Fluency\"), the School Admin sets these up first.");
                    AddBullet(c, "Rate the Student", "Instead of numbers, use a 1–5 Rating Scale or Dropdown (Excellent, Good, Fair, Needs Improvement).");
                    AddBullet(c, "Narrative Comments", "Use the \"Teacher's Comment\" box to give personalized feedback that parents can see.");
                });
                page.Footer().AlignCenter().Text("RiseFlow — Teacher's Quick Start Guide").FontSize(8).FontColor(Colors.Grey.Medium);
            });

            // Step 3: Parent Connection
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.Header().Row(r =>
                {
                    r.ConstantItem(36).Height(36).Width(36).Background(PrimaryColor).AlignCenter().AlignMiddle().Text("3").Bold().FontSize(18).FontColor(Colors.White);
                    r.RelativeItem().PaddingLeft(12).AlignMiddle().Text("The \"Parent Connection\" (Communication)").Bold().FontSize(16).FontColor(PrimaryColor);
                });
                page.Content().Column(c =>
                {
                    c.Spacing(16);
                    AddBullet(c, "Teacher Profile", "Ensure your phone number and WhatsApp link are updated in your profile.");
                    AddBullet(c, "WhatsApp Chat", "Parents can now click the WhatsApp icon on their dashboard to ask you questions about their child's performance. Don't worry—your number is only visible to parents of students in your specific class.");
                });
                page.Footer().AlignCenter().Text("RiseFlow — Teacher's Quick Start Guide").FontSize(8).FontColor(Colors.Grey.Medium);
            });

            // Troubleshooting & Tips
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.Header().Row(r =>
                {
                    r.RelativeItem().Text("Troubleshooting & Tips").Bold().FontSize(16).FontColor(Colors.Grey.Darken2);
                });
                page.Content().Column(c =>
                {
                    c.Spacing(20);
                    AddTip(c, "Red Highlights", "If a cell turns red, you have entered a score higher than the maximum (e.g., entering 80 in a 70-mark exam box).");
                    AddTip(c, "Offline Mode", "If the school internet is slow, the RiseFlow Desktop/App will save your work locally and sync as soon as you are back online.");
                    AddTip(c, "Missing Student?", "If a student isn't on your list, contact the School Admin to \"Admit\" them to the class via the Excel Import tool.");
                    c.Spacing(24);
                    c.Item().Padding(12).Background(Colors.Grey.Lighten4).Column(box =>
                    {
                        box.Item().Text("Hand this guide to your teachers after onboarding. Simple, visual, and focused on what they need.").FontSize(10).Italic().FontColor(Colors.Grey.Darken1);
                    });
                });
                page.Footer().AlignCenter().Text("RiseFlow — riseflow.com").FontSize(8).FontColor(Colors.Grey.Medium);
            });
        }).GeneratePdf();
    }

    private static void AddBullet(ColumnDescriptor c, string title, string body)
    {
        c.Item().Row(r =>
        {
            r.ConstantItem(16).PaddingTop(2).Text("•").Bold().FontSize(14).FontColor(AccentColor);
            r.RelativeItem().Column(col =>
            {
                col.Item().Text(title).Bold().FontSize(11).FontColor(PrimaryColor);
                col.Item().Text(body).FontSize(10).FontColor(Colors.Grey.Darken1);
                col.Spacing(4);
            });
        });
        c.Spacing(12);
    }

    private static void AddTip(ColumnDescriptor c, string title, string body)
    {
        c.Item().Padding(10).Background(Colors.Grey.Lighten4).Column(col =>
        {
            col.Item().Text(title).Bold().FontSize(11).FontColor(AccentColor);
            col.Item().Text(body).FontSize(10);
        });
    }
}
