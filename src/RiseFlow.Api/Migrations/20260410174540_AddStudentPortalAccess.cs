using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiseFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentPortalAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudentPortalAccesses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ShowDateOfBirth = table.Column<bool>(type: "boolean", nullable: false),
                    ShowLocationDetails = table.Column<bool>(type: "boolean", nullable: false),
                    ShowHealthDetails = table.Column<bool>(type: "boolean", nullable: false),
                    ShowEmergencyContacts = table.Column<bool>(type: "boolean", nullable: false),
                    ShowParentContactDetails = table.Column<bool>(type: "boolean", nullable: false),
                    ShowPreviousSchoolDetails = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CredentialsSharedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastPasswordResetAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentPortalAccesses");
        }
    }
}
