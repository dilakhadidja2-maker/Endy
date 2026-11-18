using Endy.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endy.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261116152000_LinkNotificationsToAccessRequests")]
    public partial class LinkNotificationsToAccessRequests : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccessRequestId",
                table: "Notifications",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_AccessRequestId",
                table: "Notifications",
                column: "AccessRequestId");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_AccessRequests_AccessRequestId",
                table: "Notifications",
                column: "AccessRequestId",
                principalTable: "AccessRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_AccessRequests_AccessRequestId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_AccessRequestId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "AccessRequestId",
                table: "Notifications");
        }
    }
}
