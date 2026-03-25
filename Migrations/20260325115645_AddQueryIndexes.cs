using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServerDistorApi.Migrations
{
    /// <inheritdoc />
    public partial class AddQueryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Servers_Status",
                table: "Servers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ServerLogs_RentEnd",
                table: "ServerLogs",
                column: "RentEnd");

            migrationBuilder.CreateIndex(
                name: "IX_ServerLogs_ServerId",
                table: "ServerLogs",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Rentals_AutoReleaseAtUtc",
                table: "Rentals",
                column: "AutoReleaseAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Rentals_ReadyAfterUtc",
                table: "Rentals",
                column: "ReadyAfterUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Rentals_ServerId",
                table: "Rentals",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Rentals_Status",
                table: "Rentals",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Servers_Status",
                table: "Servers");

            migrationBuilder.DropIndex(
                name: "IX_ServerLogs_RentEnd",
                table: "ServerLogs");

            migrationBuilder.DropIndex(
                name: "IX_ServerLogs_ServerId",
                table: "ServerLogs");

            migrationBuilder.DropIndex(
                name: "IX_Rentals_AutoReleaseAtUtc",
                table: "Rentals");

            migrationBuilder.DropIndex(
                name: "IX_Rentals_ReadyAfterUtc",
                table: "Rentals");

            migrationBuilder.DropIndex(
                name: "IX_Rentals_ServerId",
                table: "Rentals");

            migrationBuilder.DropIndex(
                name: "IX_Rentals_Status",
                table: "Rentals");
        }
    }
}
