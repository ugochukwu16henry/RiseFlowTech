using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiseFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentProfileGovernance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateTable(
                name: "StudentProfileVisibilitySettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShowDateOfBirthToTeachers = table.Column<bool>(type: "boolean", nullable: false),
                    ShowLocationDetailsToTeachers = table.Column<bool>(type: "boolean", nullable: false),
                    ShowHealthDetailsToTeachers = table.Column<bool>(type: "boolean", nullable: false),
                    ShowParentContactsToTeachers = table.Column<bool>(type: "boolean", nullable: false),
                    ShowAcademicHistoryToTeachers = table.Column<bool>(type: "boolean", nullable: false),
                    ShowPreviousRecordToTeachers = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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

            migrationBuilder.CreateIndex(
                name: "IX_StudentProfileVisibilitySettings_SchoolId",
                table: "StudentProfileVisibilitySettings",
                column: "SchoolId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentProfileVisibilitySettings");

            migrationBuilder.DropColumn(
                name: "ParentProfileLastUpdatedAtUtc",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "PreviousClass",
                table: "Students");
        }
    }
}
