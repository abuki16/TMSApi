using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TMSApi.Migrations
{
    /// <inheritdoc />
    public partial class RemoveStudentFromAssessment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Assessments_Students_StudentId",
                table: "Assessments");

            migrationBuilder.DropIndex(
                name: "IX_Assessments_StudentId",
                table: "Assessments");

            migrationBuilder.DropColumn(
                name: "ScoreObtained",
                table: "Assessments");

            migrationBuilder.DropColumn(
                name: "StudentId",
                table: "Assessments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ScoreObtained",
                table: "Assessments",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "StudentId",
                table: "Assessments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_StudentId",
                table: "Assessments",
                column: "StudentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Assessments_Students_StudentId",
                table: "Assessments",
                column: "StudentId",
                principalTable: "Students",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
