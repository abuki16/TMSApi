using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TMSApi.Migrations
{
    /// <inheritdoc />
    public partial class AddEnrollmentStatusAndStudentSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Enrollments",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "Enrollments");
        }
    }
}
