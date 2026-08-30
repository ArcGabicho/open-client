using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace openclient.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClientAnalyticsIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Clients_CreatedAt",
                table: "Clients",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_District",
                table: "Clients",
                column: "District");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_Industry",
                table: "Clients",
                column: "Industry");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_JobTitle",
                table: "Clients",
                column: "JobTitle");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_Province",
                table: "Clients",
                column: "Province");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Clients_CreatedAt",
                table: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_Clients_District",
                table: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_Clients_Industry",
                table: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_Clients_JobTitle",
                table: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_Clients_Province",
                table: "Clients");
        }
    }
}
