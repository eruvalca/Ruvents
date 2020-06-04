using Microsoft.EntityFrameworkCore.Migrations;

namespace Ruvents.Server.Data.Migrations
{
    public partial class navPropOnAttendee : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Attendees_Ruvents_RuventId",
                table: "Attendees");

            migrationBuilder.AlterColumn<int>(
                name: "RuventId",
                table: "Attendees",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Attendees_Ruvents_RuventId",
                table: "Attendees",
                column: "RuventId",
                principalTable: "Ruvents",
                principalColumn: "RuventId",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Attendees_Ruvents_RuventId",
                table: "Attendees");

            migrationBuilder.AlterColumn<int>(
                name: "RuventId",
                table: "Attendees",
                type: "int",
                nullable: true,
                oldClrType: typeof(int));

            migrationBuilder.AddForeignKey(
                name: "FK_Attendees_Ruvents_RuventId",
                table: "Attendees",
                column: "RuventId",
                principalTable: "Ruvents",
                principalColumn: "RuventId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
