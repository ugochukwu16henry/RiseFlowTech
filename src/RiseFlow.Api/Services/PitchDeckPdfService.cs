using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace RiseFlow.Api.Services;

/// <summary>
/// Generates the RiseFlow "Future-Ready" School Pitch Deck as a downloadable PDF.
/// </summary>
public class PitchDeckPdfService
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
                page.Background(Colors.White);
                page.Content().Column(c =>
                {
                    c.Spacing(120);
                    c.Item().AlignCenter().Text("RiseFlow").Bold().FontSize(36).FontColor(PrimaryColor);
                    c.Item().AlignCenter().Text("The \"Future-Ready\" School").FontSize(22).FontColor(Colors.Grey.Darken2);
                    c.Item().AlignCenter().Text("Pitch Deck").FontSize(18).FontColor(AccentColor);
                    c.Spacing(80);
                    c.Item().AlignCenter().Text("One platform. Total control. Built for African schools.").FontSize(12).FontColor(Colors.Grey.Medium);
                });
                page.Footer().AlignCenter().Text("riseflow.com").FontSize(9).FontColor(Colors.Grey.Medium);
            });

            AddSlide(container, 1, "The Problem (The Paper Burden)",
                "The Reality:",
                "Your teachers spend 40% of their time writing on paper instead of teaching.",
                "The Risk:",
                "Paper records get lost, burnt, or eaten by termites. In 5 years, can you prove a student graduated?",
                "The Communication Gap:",
                "Parents only see you during PTA meetings or when results are out. They feel disconnected.");

            AddSlide(container, 2, "The Solution (Introducing RiseFlow)",
                "One Platform, Total Control:",
                "Manage students, teachers, and results from your phone or laptop.",
                "Instant Digitalization:",
                "Upload your entire school list in 5 minutes via Excel.",
                "Brand Identity:",
                "Your school logo, your school name, and your official digital stamp on every document.");

            AddSlide(container, 3, "Why Parents Will Love Your School",
                "The Parent Hub:",
                "A dedicated app where parents see their child's progress in real-time.",
                "Teacher Access:",
                "Direct links to chat with teachers on WhatsApp—no more searching for phone numbers.",
                "Multi-Child Management:",
                "One login for parents with 2, 3, or 4 children in your school.",
                "Digital Results:",
                "Parents get notified the second results are approved. No more \"lost\" result sheets.");

            AddSlide(container, 4, "Why Teachers Will Be More Productive",
                "Secondary School:",
                "Automatic ranking (1st, 2nd, 3rd) and grade calculation (A1, B2, etc.).",
                "Primary School:",
                "Customizable \"Social Habits\" and \"Psychomotor\" tracking.",
                "Zero Math Errors:",
                "The system does the summing; the teacher just enters the scores.");

            AddSlide(container, 5, "The \"Digital Transcript\" (Your Competitive Edge)",
                "Future-Proof:",
                "Students can request transcripts for foreign universities or other schools instantly.",
                "QR Verification:",
                "Every document has a QR code. Anyone can scan it to verify it's an authentic record from your school.",
                "Government Ready:",
                "Aligned with NDPR data laws and ready for future government plugins.");

            AddSlide(container, 6, "Pricing (The \"Fair-Growth\" Model)",
                "Start for Free:",
                "Your first 50 students are ₦0.00. We grow only when you grow.",
                "Affordable Scaling:",
                "Only ₦500 per student after the first 50.",
                "No Hidden Fees:",
                "Includes the web portal, the mobile app, and all future updates.");

            // How to deliver + pro tip
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.Header().Row(r =>
                {
                    r.RelativeItem().Text("How to Deliver the Pitch").Bold().FontSize(16).FontColor(PrimaryColor);
                    r.ConstantItem(60).AlignRight().Text("7").FontSize(12).FontColor(Colors.Grey.Medium);
                });
                page.Content().Column(c =>
                {
                    c.Spacing(12);
                    c.Item().Text("The \"5-Minute Import\" Demo").Bold().FontSize(12).FontColor(AccentColor);
                    c.Item().PaddingBottom(8).Text("Bring a laptop. Ask them for an Excel list of one class. Import it right there in front of them. Their eyes will light up when they see the students appear instantly.");
                    c.Item().Text("The \"WhatsApp\" Test").Bold().FontSize(12).FontColor(AccentColor);
                    c.Item().PaddingBottom(8).Text("Show them how a parent can click a button on the app and start a chat with the Class Teacher.");
                    c.Item().Text("The \"Paper vs. Digital\" Comparison").Bold().FontSize(12).FontColor(AccentColor);
                    c.Item().PaddingBottom(16).Text("Hold a physical, tattered result book in one hand and your tablet with RiseFlow in the other. Ask: \"Which one looks like a 21st-century school?\"");
                    c.Item().PaddingTop(16).Text("Final Pro-Tip for You").Bold().FontSize(14).FontColor(PrimaryColor);
                    c.Item().PaddingTop(4).Text("In Nigeria, \"Proprietors\" often talk to each other. If you get one big school in a local government area, the others will follow because they don't want to be \"the school using paper\" while their neighbor is \"digital.\"").Italic();
                });
                page.Footer().AlignCenter().Text("RiseFlow — Future-ready schools across Africa").FontSize(9).FontColor(Colors.Grey.Medium);
            });
        }).GeneratePdf();
    }

    private static void AddSlide(IDocumentContainer container, int slideNum, string title, params string[] items)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(40);
            page.Header().Row(r =>
            {
                r.RelativeItem().Text(title).Bold().FontSize(18).FontColor(PrimaryColor);
                r.ConstantItem(60).AlignRight().Text(slideNum.ToString()).FontSize(12).FontColor(Colors.Grey.Medium);
            });
            page.Content().Column(c =>
            {
                c.Spacing(20);
                for (var i = 0; i < items.Length; i++)
                {
                    var isLabel = i % 2 == 0 && i + 1 < items.Length;
                    if (isLabel)
                        c.Item().Text(items[i]).Bold().FontSize(11).FontColor(AccentColor);
                    else
                        c.Item().PaddingLeft(16).PaddingBottom(10).Text(items[i]).FontSize(11);
                }
            });
            page.Footer().AlignCenter().Text("RiseFlow").FontSize(9).FontColor(Colors.Grey.Medium);
        });
    }
}
