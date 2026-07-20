using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WaterTemperature.Api.Migrations
{
    /// <inheritdoc />
    public partial class HomeAssistantMqttIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PushToHomeAssistant",
                table: "Devices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "HomeAssistantDeviceName",
                table: "Devices",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HomeAssistantIntegrationSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    Host = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Port = table.Column<int>(type: "integer", nullable: false),
                    Username = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    PasswordProtected = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeAssistantIntegrationSettings", x => x.Id);
                    table.CheckConstraint("CK_HomeAssistantIntegrationSettings_Singleton", "\"Id\" = 1");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HomeAssistantIntegrationSettings");

            migrationBuilder.DropColumn(
                name: "HomeAssistantDeviceName",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "PushToHomeAssistant",
                table: "Devices");
        }
    }
}
