using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiseFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAcademicTermCalendarFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "AcademicTerms",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "MidtermBreakEnd",
                table: "AcademicTerms",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "MidtermBreakStart",
                table: "AcademicTerms",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "AcademicTerms",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "AcademicTerms");

            migrationBuilder.DropColumn(
                name: "MidtermBreakEnd",
                table: "AcademicTerms");

            migrationBuilder.DropColumn(
                name: "MidtermBreakStart",
                table: "AcademicTerms");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "AcademicTerms");
        }
    }
}
