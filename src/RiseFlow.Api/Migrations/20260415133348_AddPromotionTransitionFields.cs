using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiseFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPromotionTransitionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AcademicSystemProfileId",
                table: "Schools",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerName",
                table: "Schools",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromotionTransitionOverrideJson",
                table: "Schools",
                type: "character varying(12000)",
                maxLength: 12000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationDocumentPath",
                table: "Schools",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SchoolAdminName",
                table: "Schools",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppNumber",
                table: "Schools",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "FileBytes",
                table: "FileAssets",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "HeadshotBytes",
                table: "Affiliates",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HeadshotContentType",
                table: "Affiliates",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AcademicSystemProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    SuggestedTermsPerYear = table.Column<int>(type: "integer", nullable: true),
                    GradeTemplatesJson = table.Column<string>(type: "character varying(12000)", maxLength: 12000, nullable: false),
                    StageOrderJson = table.Column<string>(type: "character varying(12000)", maxLength: 12000, nullable: true),
                    PromotionTransitionJson = table.Column<string>(type: "character varying(12000)", maxLength: 12000, nullable: true),
                    DefaultGradingScaleCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademicSystemProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AffiliateTrainingCompletions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AffiliateId = table.Column<Guid>(type: "uuid", nullable: false),
                    TrainingVideoId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffiliateTrainingCompletions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AffiliateTrainingCompletions_AffiliateTrainingVideos_Traini~",
                        column: x => x.TrainingVideoId,
                        principalTable: "AffiliateTrainingVideos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AffiliateTrainingCompletions_Affiliates_AffiliateId",
                        column: x => x.AffiliateId,
                        principalTable: "Affiliates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClassPromotionRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromClassId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToClassId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromTermId = table.Column<Guid>(type: "uuid", nullable: true),
                    PromotionSessionLabel = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Notes = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassPromotionRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClassPromotionRequests_AcademicTerms_FromTermId",
                        column: x => x.FromTermId,
                        principalTable: "AcademicTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ClassPromotionRequests_Classes_FromClassId",
                        column: x => x.FromClassId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassPromotionRequests_Classes_ToClassId",
                        column: x => x.ToClassId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassPromotionRequests_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClassPromotionRequests_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClassPromotionRequestItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassPromotionRequestItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClassPromotionRequestItems_ClassPromotionRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "ClassPromotionRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClassPromotionRequestItems_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Schools_AcademicSystemProfileId",
                table: "Schools",
                column: "AcademicSystemProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicSystemProfiles_Code",
                table: "AcademicSystemProfiles",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateTrainingCompletions_AffiliateId_TrainingVideoId",
                table: "AffiliateTrainingCompletions",
                columns: new[] { "AffiliateId", "TrainingVideoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateTrainingCompletions_TrainingVideoId",
                table: "AffiliateTrainingCompletions",
                column: "TrainingVideoId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassPromotionRequestItems_RequestId_StudentId",
                table: "ClassPromotionRequestItems",
                columns: new[] { "RequestId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClassPromotionRequestItems_SchoolId_StudentId",
                table: "ClassPromotionRequestItems",
                columns: new[] { "SchoolId", "StudentId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassPromotionRequestItems_StudentId",
                table: "ClassPromotionRequestItems",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassPromotionRequests_FromClassId",
                table: "ClassPromotionRequests",
                column: "FromClassId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassPromotionRequests_FromTermId",
                table: "ClassPromotionRequests",
                column: "FromTermId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassPromotionRequests_SchoolId_Status_RequestedAtUtc",
                table: "ClassPromotionRequests",
                columns: new[] { "SchoolId", "Status", "RequestedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassPromotionRequests_TeacherId",
                table: "ClassPromotionRequests",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassPromotionRequests_ToClassId",
                table: "ClassPromotionRequests",
                column: "ToClassId");

            migrationBuilder.AddForeignKey(
                name: "FK_Schools_AcademicSystemProfiles_AcademicSystemProfileId",
                table: "Schools",
                column: "AcademicSystemProfileId",
                principalTable: "AcademicSystemProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Schools_AcademicSystemProfiles_AcademicSystemProfileId",
                table: "Schools");

            migrationBuilder.DropTable(
                name: "AcademicSystemProfiles");

            migrationBuilder.DropTable(
                name: "AffiliateTrainingCompletions");

            migrationBuilder.DropTable(
                name: "ClassPromotionRequestItems");

            migrationBuilder.DropTable(
                name: "ClassPromotionRequests");

            migrationBuilder.DropIndex(
                name: "IX_Schools_AcademicSystemProfileId",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "AcademicSystemProfileId",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "OwnerName",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "PromotionTransitionOverrideJson",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "RegistrationDocumentPath",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "SchoolAdminName",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "WhatsAppNumber",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "FileBytes",
                table: "FileAssets");

            migrationBuilder.DropColumn(
                name: "HeadshotBytes",
                table: "Affiliates");

            migrationBuilder.DropColumn(
                name: "HeadshotContentType",
                table: "Affiliates");
        }
    }
}
