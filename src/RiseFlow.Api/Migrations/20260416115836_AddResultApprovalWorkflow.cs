using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiseFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddResultApprovalWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StudentResults_SchoolId",
                table: "StudentResults");

            migrationBuilder.AddColumn<DateTime>(
                name: "FinalApprovedAtUtc",
                table: "StudentResults",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FinalApprovedByUserId",
                table: "StudentResults",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockedAtUtc",
                table: "StudentResults",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewComment",
                table: "StudentResults",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAtUtc",
                table: "StudentResults",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedByUserId",
                table: "StudentResults",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAtUtc",
                table: "StudentResults",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubmittedByTeacherId",
                table: "StudentResults",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkflowStatus",
                table: "StudentResults",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_StudentResults_SchoolId_TermId_WorkflowStatus",
                table: "StudentResults",
                columns: new[] { "SchoolId", "TermId", "WorkflowStatus" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StudentResults_SchoolId_TermId_WorkflowStatus",
                table: "StudentResults");

            migrationBuilder.DropColumn(
                name: "FinalApprovedAtUtc",
                table: "StudentResults");

            migrationBuilder.DropColumn(
                name: "FinalApprovedByUserId",
                table: "StudentResults");

            migrationBuilder.DropColumn(
                name: "LockedAtUtc",
                table: "StudentResults");

            migrationBuilder.DropColumn(
                name: "ReviewComment",
                table: "StudentResults");

            migrationBuilder.DropColumn(
                name: "ReviewedAtUtc",
                table: "StudentResults");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "StudentResults");

            migrationBuilder.DropColumn(
                name: "SubmittedAtUtc",
                table: "StudentResults");

            migrationBuilder.DropColumn(
                name: "SubmittedByTeacherId",
                table: "StudentResults");

            migrationBuilder.DropColumn(
                name: "WorkflowStatus",
                table: "StudentResults");

            migrationBuilder.CreateIndex(
                name: "IX_StudentResults_SchoolId",
                table: "StudentResults",
                column: "SchoolId");
        }
    }
}
