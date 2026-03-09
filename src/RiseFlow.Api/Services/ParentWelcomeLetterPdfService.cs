using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace RiseFlow.Api.Services;

/// <summary>
/// Generates Parent Welcome &amp; Onboarding Letters (one page per student) with NDPA 2023 consent.
/// For use by School Admin to print and hand to parents.
/// </summary>
public class ParentWelcomeLetterPdfService
{
    private const string PrimaryColor = "#1E40AF";
    private const string AccentColor = "#059669";

    public byte[] GeneratePdf(string schoolName, byte[]? logoBytes, IReadOnlyList<(string StudentFullName, string AccessCode)> students, DateTime date)
    {
        if (students.Count == 0)
            return Array.Empty<byte>();

        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(container =>
        {
            foreach (var (studentFullName, accessCode) in students)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(35);
                    page.PageColor(Colors.White);

                    // School name & logo
                    page.Header().Column(c =>
                    {
                        if (logoBytes != null && logoBytes.Length > 0)
                        {
                            try
                            {
                                c.Item().AlignCenter().MaxHeight(50).Image(logoBytes).FitArea();
                                c.Spacing(6);
                            }
                            catch { /* ignore */ }
                        }
                        c.Item().AlignCenter().Text(schoolName).Bold().FontSize(16).FontColor(PrimaryColor);
                        c.Item().AlignCenter().Text($"Date: {date:dd MMMM yyyy}").FontSize(10).FontColor(Colors.Grey.Darken1);
                    });

                    page.Content().Column(c =>
                    {
                        c.Spacing(8);
                        c.Item().Text("Subject: Transition to RiseFlow Digital School Management Portal").FontSize(10).SemiBold().FontColor(Colors.Grey.Darken2);
                        c.Spacing(8);
                        c.Item().Text("Dear Parents and Guardians,").FontSize(11);
                        c.Spacing(12);
                        c.Item().Text($"We are excited to announce that {schoolName} is moving to RiseFlow, a modern digital platform designed to bring you closer to your child's education. With RiseFlow, you can now access results, track attendance, and communicate directly with teachers—all from your mobile phone.").FontSize(10);
                        c.Spacing(16);

                        c.Item().Text("1. How to Get Started").Bold().FontSize(11).FontColor(AccentColor);
                        c.Spacing(6);
                        c.Item().Text("Download the App: Search for \"RiseFlow\" on the Google Play Store or Apple App Store, or visit www.riseflow.com.").FontSize(10);
                        c.Item().Text("Create Your Account: Sign up using your Email Address and Phone Number.").FontSize(10);
                        c.Item().Text("Link Your Child: Once logged in, click on \"Add Child\" and enter the unique Access Code provided below.").FontSize(10);
                        c.Spacing(10);

                        // Student name & access code in a prominent box
                        c.Item().Padding(12).Background(Colors.Grey.Lighten4).Column(box =>
                        {
                            box.Item().Text($"Student: {studentFullName}").Bold().FontSize(11);
                            box.Spacing(6);
                            box.Item().Text("Your Child's Access Code:").FontSize(10);
                            box.Item().Text(accessCode).Bold().FontSize(18).FontColor(PrimaryColor);
                        });
                        c.Item().PaddingTop(4).Text("(If you have multiple children in the school, you can add all of them to this single account using their respective codes.)").FontSize(9).Italic().FontColor(Colors.Grey.Darken1);
                        c.Spacing(14);

                        c.Item().Text("2. What You Can Do on RiseFlow").Bold().FontSize(11).FontColor(AccentColor);
                        c.Spacing(6);
                        c.Item().Text("• View Academic Results: Instantly access termly report cards and past transcripts.").FontSize(10);
                        c.Item().Text("• Teacher Directory: See the names and contact details (WhatsApp/Email) of the teachers responsible for your child.").FontSize(10);
                        c.Item().Text("• Real-time Notifications: Get alerts for school announcements, fee updates, and emergency notices.").FontSize(10);
                        c.Spacing(14);

                        c.Item().Text("3. Data Protection & Consent (NDPA 2023 Compliance)").Bold().FontSize(11).FontColor(AccentColor);
                        c.Spacing(6);
                        c.Item().Padding(8).Background(Colors.Grey.Lighten4).Column(consent =>
                        {
                            consent.Item().Text($"At {schoolName}, we take your child's privacy seriously. By using RiseFlow, you consent to the digital processing of your child's academic and personal data (Name, DOB, NIN, and Grades) for educational purposes only. This data is encrypted and stored securely in accordance with the Nigeria Data Protection Act (NDPA) and the Nigeria Data Protection Commission (NDPC) regulations.").FontSize(9);
                            consent.Spacing(4);
                            consent.Item().Text("You have the right to access, correct, or request the deletion of this data at any time through the school administration.").FontSize(9);
                        });
                        c.Spacing(20);
                        c.Item().Row(r =>
                        {
                            r.RelativeItem().Column(sig =>
                            {
                                sig.Item().LineHorizontal(1).LineColor(Colors.Grey.Medium);
                                sig.Item().Text("Principal's Signature").FontSize(9).FontColor(Colors.Grey.Darken1);
                            });
                            r.ConstantItem(40);
                            r.RelativeItem().Column(stamp =>
                            {
                                stamp.Item().LineHorizontal(1).LineColor(Colors.Grey.Medium);
                                stamp.Item().Text("School Stamp").FontSize(9).FontColor(Colors.Grey.Darken1);
                            });
                        });
                    });

                    page.Footer().AlignCenter().Text("RiseFlow — Parent Welcome & Onboarding Letter").FontSize(8).FontColor(Colors.Grey.Medium);
                });
            }
        }).GeneratePdf();
    }
}
