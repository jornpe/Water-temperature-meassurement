using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace WaterTemperature.Api.Migrations
{
    /// <inheritdoc />
    public partial class DeviceDetailsAndLogging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DesiredConfigurationUpdatedAtUtc",
                table: "Devices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DesiredConfigurationVersion",
                table: "Devices",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "ReportedConfigurationVersion",
                table: "Devices",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReportedReportIntervalSeconds",
                table: "Devices",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RuntimeConfigurationReportedAtUtc",
                table: "Devices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DeviceLogEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeviceId = table.Column<int>(type: "integer", nullable: false),
                    SequenceNumber = table.Column<long>(type: "bigint", nullable: false),
                    Level = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Message = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    DeviceTimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeviceUptimeMs = table.Column<long>(type: "bigint", nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceLogEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceLogEntries_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceLogEntries_DeviceId_ReceivedAtUtc",
                table: "DeviceLogEntries",
                columns: new[] { "DeviceId", "ReceivedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceLogEntries_DeviceId_SequenceNumber",
                table: "DeviceLogEntries",
                columns: new[] { "DeviceId", "SequenceNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceLogEntries");

            migrationBuilder.DropColumn(
                name: "DesiredConfigurationUpdatedAtUtc",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "DesiredConfigurationVersion",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "ReportedConfigurationVersion",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "ReportedReportIntervalSeconds",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "RuntimeConfigurationReportedAtUtc",
                table: "Devices");
        }
    }
}
