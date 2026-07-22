using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace WaterTemperature.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceBatteryHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "LatestBatteryAdcVoltage",
                table: "Devices",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LatestBatteryAtUtc",
                table: "Devices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LatestBatteryChargeState",
                table: "Devices",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LatestBatteryModemMillivolts",
                table: "Devices",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LatestBatteryModemReadingValid",
                table: "Devices",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LatestBatteryPercentage",
                table: "Devices",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LatestBatteryState",
                table: "Devices",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DeviceBatteryHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeviceId = table.Column<int>(type: "integer", nullable: false),
                    ModemReadingValid = table.Column<bool>(type: "boolean", nullable: false),
                    ChargeState = table.Column<int>(type: "integer", nullable: false),
                    BatteryState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Percentage = table.Column<int>(type: "integer", nullable: false),
                    ModemMillivolts = table.Column<int>(type: "integer", nullable: false),
                    AdcVoltage = table.Column<float>(type: "real", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceBatteryHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceBatteryHistory_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceBatteryHistory_DeviceId_RecordedAtUtc",
                table: "DeviceBatteryHistory",
                columns: new[] { "DeviceId", "RecordedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceBatteryHistory");

            migrationBuilder.DropColumn(
                name: "LatestBatteryAdcVoltage",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "LatestBatteryAtUtc",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "LatestBatteryChargeState",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "LatestBatteryModemMillivolts",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "LatestBatteryModemReadingValid",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "LatestBatteryPercentage",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "LatestBatteryState",
                table: "Devices");
        }
    }
}
