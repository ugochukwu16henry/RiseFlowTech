using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiseFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class SyncPostgresAsPrimaryDataStore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentPortalAccesses");

            migrationBuilder.DropTable(
                name: "StudentProfileVisibilitySettings");

            migrationBuilder.DropTable(
                name: "TeacherCustomFieldValues");

            migrationBuilder.DropTable(
                name: "TeacherProfileFieldSettings");

            migrationBuilder.DropIndex(
                name: "IX_AffiliateCommissionLedgers_BillingRecordId",
                table: "AffiliateCommissionLedgers");

            migrationBuilder.DropColumn(
                name: "Religion",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "ParentProfileLastUpdatedAtUtc",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "PreviousClass",
                table: "Students");

            migrationBuilder.AlterColumn<string>(
                name: "YoutubeUrl",
                table: "AffiliateTrainingVideos",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(512)",
                oldMaxLength: 512);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "AffiliateTrainingVideos",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(160)",
                oldMaxLength: 160);

            migrationBuilder.AlterColumn<string>(
                name: "UniqueCode",
                table: "Affiliates",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "Affiliates",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(512)",
                oldMaxLength: 512,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AccountNumber",
                table: "Affiliates",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "AffiliateNotifications",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(160)",
                oldMaxLength: 160);

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "AffiliateLeadRequests",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(512)",
                oldMaxLength: 512,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CommissionType",
                table: "AffiliateCommissionLedgers",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateCommissionLedgers_BillingRecordId",
                table: "AffiliateCommissionLedgers",
                column: "BillingRecordId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AffiliateCommissionLedgers_BillingRecordId",
                table: "AffiliateCommissionLedgers");

            migrationBuilder.AddColumn<string>(
                name: "Religion",
                table: "Teachers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ParentProfileLastUpdatedAtUtc",
                table: "Students",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousClass",
                table: "Students",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "YoutubeUrl",
                table: "AffiliateTrainingVideos",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1024)",
                oldMaxLength: 1024);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "AffiliateTrainingVideos",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "UniqueCode",
                table: "Affiliates",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "Affiliates",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AccountNumber",
                table: "Affiliates",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "AffiliateNotifications",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "AffiliateLeadRequests",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CommissionType",
                table: "AffiliateCommissionLedgers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.CreateTable(
                name: "StudentPortalAccesses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CredentialsSharedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastPasswordResetAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LoginId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ShowDateOfBirth = table.Column<bool>(type: "boolean", nullable: false),
                    ShowEmergencyContacts = table.Column<bool>(type: "boolean", nullable: false),
                    ShowHealthDetails = table.Column<bool>(type: "boolean", nullable: false),
                    ShowLocationDetails = table.Column<bool>(type: "boolean", nullable: false),
                    ShowParentContactDetails = table.Column<bool>(type: "boolean", nullable: false),
                    ShowPreviousSchoolDetails = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentPortalAccesses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentPortalAccesses_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentPortalAccesses_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentPortalAccesses_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentProfileVisibilitySettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ShowAcademicHistoryToTeachers = table.Column<bool>(type: "boolean", nullable: false),
                    ShowDateOfBirthToTeachers = table.Column<bool>(type: "boolean", nullable: false),
                    ShowHealthDetailsToTeachers = table.Column<bool>(type: "boolean", nullable: false),
                    ShowLocationDetailsToTeachers = table.Column<bool>(type: "boolean", nullable: false),
                    ShowParentContactsToTeachers = table.Column<bool>(type: "boolean", nullable: false),
                    ShowPreviousRecordToTeachers = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentProfileVisibilitySettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentProfileVisibilitySettings_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeacherCustomFieldValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FieldKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Value = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherCustomFieldValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeacherCustomFieldValues_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeacherCustomFieldValues_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeacherProfileFieldSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    FieldKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IsAdminOnly = table.Column<bool>(type: "boolean", nullable: false),
                    IsCustom = table.Column<bool>(type: "boolean", nullable: false),
                    IsEditableByTeacher = table.Column<bool>(type: "boolean", nullable: false),
                    IsVisibleToTeacher = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherProfileFieldSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeacherProfileFieldSettings_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AffiliateCommissionLedgers_BillingRecordId",
                table: "AffiliateCommissionLedgers",
                column: "BillingRecordId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentPortalAccesses_SchoolId_LoginId",
                table: "StudentPortalAccesses",
                columns: new[] { "SchoolId", "LoginId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentPortalAccesses_StudentId",
                table: "StudentPortalAccesses",
                column: "StudentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentPortalAccesses_UserId",
                table: "StudentPortalAccesses",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentProfileVisibilitySettings_SchoolId",
                table: "StudentProfileVisibilitySettings",
                column: "SchoolId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeacherCustomFieldValues_SchoolId",
                table: "TeacherCustomFieldValues",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherCustomFieldValues_TeacherId_FieldKey",
                table: "TeacherCustomFieldValues",
                columns: new[] { "TeacherId", "FieldKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeacherProfileFieldSettings_SchoolId_FieldKey",
                table: "TeacherProfileFieldSettings",
                columns: new[] { "SchoolId", "FieldKey" },
                unique: true);
        }
    }
}
