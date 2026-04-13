using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiseFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSchoolFeesSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SchoolBankDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    BankName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AccountName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AccountNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    BranchOrSortCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PaymentInstructions = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolBankDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchoolBankDetails_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TermFeeSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    TermLabel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AcademicYear = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    GradeId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClassId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TermFeeSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TermFeeSchedules_Classes_ClassId",
                        column: x => x.ClassId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TermFeeSchedules_Grades_GradeId",
                        column: x => x.GradeId,
                        principalTable: "Grades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TermFeeSchedules_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FeePaymentRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReceiptFilePath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ReceiptFileName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ParentNote = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    AdminNote = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    SubmittedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeePaymentRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeePaymentRecords_Parents_ParentId",
                        column: x => x.ParentId,
                        principalTable: "Parents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_FeePaymentRecords_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FeePaymentRecords_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FeePaymentRecords_TermFeeSchedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "TermFeeSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeePaymentRecords_ParentId",
                table: "FeePaymentRecords",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_FeePaymentRecords_ScheduleId",
                table: "FeePaymentRecords",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_FeePaymentRecords_SchoolId_ScheduleId_StudentId",
                table: "FeePaymentRecords",
                columns: new[] { "SchoolId", "ScheduleId", "StudentId" });

            migrationBuilder.CreateIndex(
                name: "IX_FeePaymentRecords_SchoolId_Status",
                table: "FeePaymentRecords",
                columns: new[] { "SchoolId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_FeePaymentRecords_StudentId",
                table: "FeePaymentRecords",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolBankDetails_SchoolId",
                table: "SchoolBankDetails",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_TermFeeSchedules_ClassId",
                table: "TermFeeSchedules",
                column: "ClassId");

            migrationBuilder.CreateIndex(
                name: "IX_TermFeeSchedules_GradeId",
                table: "TermFeeSchedules",
                column: "GradeId");

            migrationBuilder.CreateIndex(
                name: "IX_TermFeeSchedules_SchoolId_TermLabel_AcademicYear_GradeId_Cl~",
                table: "TermFeeSchedules",
                columns: new[] { "SchoolId", "TermLabel", "AcademicYear", "GradeId", "ClassId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeePaymentRecords");

            migrationBuilder.DropTable(
                name: "SchoolBankDetails");

            migrationBuilder.DropTable(
                name: "TermFeeSchedules");
        }
    }
}
