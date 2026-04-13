using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiseFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAcademicOpsAndCommunications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.CreateTable(
                name: "ClassRoutines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: true),
                    Weekday = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    EndTime = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    Room = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassRoutines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClassRoutines_Classes_ClassId",
                        column: x => x.ClassId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClassRoutines_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClassRoutines_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassRoutines_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Exams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ClassId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    TermId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByTeacherId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Exams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Exams_AcademicTerms_TermId",
                        column: x => x.TermId,
                        principalTable: "AcademicTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Exams_Classes_ClassId",
                        column: x => x.ClassId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Exams_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Exams_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Exams_Teachers_CreatedByTeacherId",
                        column: x => x.CreatedByTeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "GradingSystems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ClassId = table.Column<Guid>(type: "uuid", nullable: true),
                    TermId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GradingSystems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GradingSystems_AcademicTerms_TermId",
                        column: x => x.TermId,
                        principalTable: "AcademicTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GradingSystems_Classes_ClassId",
                        column: x => x.ClassId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GradingSystems_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MarkSubmissionWindows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    TermId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsOpen = table.Column<bool>(type: "boolean", nullable: false),
                    OpenedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarkSubmissionWindows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarkSubmissionWindows_AcademicTerms_TermId",
                        column: x => x.TermId,
                        principalTable: "AcademicTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarkSubmissionWindows_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SchoolEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    StartAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ColorHex = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchoolEvents_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SchoolNotices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Body = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    TargetRolesCsv = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PublishedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PublishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolNotices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchoolNotices_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentPromotions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromClassId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToClassId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromTermId = table.Column<Guid>(type: "uuid", nullable: true),
                    PromotionSessionLabel = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PromotedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PromotedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentPromotions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentPromotions_AcademicTerms_FromTermId",
                        column: x => x.FromTermId,
                        principalTable: "AcademicTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_StudentPromotions_Classes_FromClassId",
                        column: x => x.FromClassId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentPromotions_Classes_ToClassId",
                        column: x => x.ToClassId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentPromotions_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentPromotions_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeacherAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    TermId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    FileAssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    DueDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeacherAssignments_AcademicTerms_TermId",
                        column: x => x.TermId,
                        principalTable: "AcademicTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherAssignments_Classes_ClassId",
                        column: x => x.ClassId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherAssignments_FileAssets_FileAssetId",
                        column: x => x.FileAssetId,
                        principalTable: "FileAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherAssignments_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeacherAssignments_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherAssignments_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GradeRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    GradingSystemId = table.Column<Guid>(type: "uuid", nullable: false),
                    GradeLetter = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    MinPercent = table.Column<decimal>(type: "numeric", nullable: false),
                    MaxPercent = table.Column<decimal>(type: "numeric", nullable: false),
                    GradePoint = table.Column<decimal>(type: "numeric", nullable: true),
                    Remarks = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GradeRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GradeRules_GradingSystems_GradingSystemId",
                        column: x => x.GradingSystemId,
                        principalTable: "GradingSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GradeRules_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentResults_ExamId",
                table: "StudentResults",
                column: "ExamId");

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateCommissionLedgers_BillingRecordId",
                table: "AffiliateCommissionLedgers",
                column: "BillingRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassRoutines_ClassId",
                table: "ClassRoutines",
                column: "ClassId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassRoutines_SchoolId_ClassId_Weekday_StartTime_EndTime",
                table: "ClassRoutines",
                columns: new[] { "SchoolId", "ClassId", "Weekday", "StartTime", "EndTime" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassRoutines_SchoolId_TeacherId_Weekday_StartTime",
                table: "ClassRoutines",
                columns: new[] { "SchoolId", "TeacherId", "Weekday", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassRoutines_SubjectId",
                table: "ClassRoutines",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassRoutines_TeacherId",
                table: "ClassRoutines",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_Exams_ClassId",
                table: "Exams",
                column: "ClassId");

            migrationBuilder.CreateIndex(
                name: "IX_Exams_CreatedByTeacherId",
                table: "Exams",
                column: "CreatedByTeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_Exams_SchoolId_TermId_ClassId_SubjectId",
                table: "Exams",
                columns: new[] { "SchoolId", "TermId", "ClassId", "SubjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_Exams_SubjectId",
                table: "Exams",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Exams_TermId",
                table: "Exams",
                column: "TermId");

            migrationBuilder.CreateIndex(
                name: "IX_GradeRules_GradingSystemId_MinPercent_MaxPercent",
                table: "GradeRules",
                columns: new[] { "GradingSystemId", "MinPercent", "MaxPercent" });

            migrationBuilder.CreateIndex(
                name: "IX_GradeRules_SchoolId",
                table: "GradeRules",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_GradingSystems_ClassId",
                table: "GradingSystems",
                column: "ClassId");

            migrationBuilder.CreateIndex(
                name: "IX_GradingSystems_SchoolId_ClassId_TermId_Name",
                table: "GradingSystems",
                columns: new[] { "SchoolId", "ClassId", "TermId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_GradingSystems_TermId",
                table: "GradingSystems",
                column: "TermId");

            migrationBuilder.CreateIndex(
                name: "IX_MarkSubmissionWindows_SchoolId_TermId",
                table: "MarkSubmissionWindows",
                columns: new[] { "SchoolId", "TermId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarkSubmissionWindows_TermId",
                table: "MarkSubmissionWindows",
                column: "TermId");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolEvents_SchoolId_StartAtUtc",
                table: "SchoolEvents",
                columns: new[] { "SchoolId", "StartAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SchoolNotices_SchoolId_IsActive_PublishedAtUtc",
                table: "SchoolNotices",
                columns: new[] { "SchoolId", "IsActive", "PublishedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentPromotions_FromClassId",
                table: "StudentPromotions",
                column: "FromClassId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentPromotions_FromTermId",
                table: "StudentPromotions",
                column: "FromTermId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentPromotions_SchoolId_StudentId_FromClassId_FromTermId",
                table: "StudentPromotions",
                columns: new[] { "SchoolId", "StudentId", "FromClassId", "FromTermId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentPromotions_StudentId",
                table: "StudentPromotions",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentPromotions_ToClassId",
                table: "StudentPromotions",
                column: "ToClassId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherAssignments_ClassId",
                table: "TeacherAssignments",
                column: "ClassId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherAssignments_FileAssetId",
                table: "TeacherAssignments",
                column: "FileAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherAssignments_SchoolId_ClassId_TermId_SubjectId",
                table: "TeacherAssignments",
                columns: new[] { "SchoolId", "ClassId", "TermId", "SubjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherAssignments_SubjectId",
                table: "TeacherAssignments",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherAssignments_TeacherId",
                table: "TeacherAssignments",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherAssignments_TermId",
                table: "TeacherAssignments",
                column: "TermId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClassRoutines");

            migrationBuilder.DropTable(
                name: "GradeRules");

            migrationBuilder.DropTable(
                name: "MarkSubmissionWindows");

            migrationBuilder.DropTable(
                name: "SchoolEvents");

            migrationBuilder.DropTable(
                name: "SchoolNotices");

            migrationBuilder.DropTable(
                name: "StudentPromotions");

            migrationBuilder.DropTable(
                name: "TeacherAssignments");

            migrationBuilder.DropTable(
                name: "Exams");

            migrationBuilder.DropTable(
                name: "GradingSystems");
        }
    }
}
