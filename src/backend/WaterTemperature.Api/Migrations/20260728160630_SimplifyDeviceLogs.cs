using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WaterTemperature.Api.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyDeviceLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DeviceLogEntries_DeviceId_SequenceNumber",
                table: "DeviceLogEntries");

            migrationBuilder.DropIndex(
                name: "IX_DeviceLogEntries_DeviceId_ReceivedAtUtc_Id",
                table: "DeviceLogEntries");

            migrationBuilder.AddColumn<DateTime>(
                name: "TimestampUtc",
                table: "DeviceLogEntries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "DeviceLogEntries"
                SET "TimestampUtc" = COALESCE("DeviceTimestampUtc", "ReceivedAtUtc");
                """);

            migrationBuilder.AlterColumn<DateTime>(
                name: "TimestampUtc",
                table: "DeviceLogEntries",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "DeviceTimestampUtc",
                table: "DeviceLogEntries");

            migrationBuilder.DropColumn(
                name: "DeviceUptimeMs",
                table: "DeviceLogEntries");

            migrationBuilder.DropColumn(
                name: "ReceivedAtUtc",
                table: "DeviceLogEntries");

            migrationBuilder.DropColumn(
                name: "SequenceNumber",
                table: "DeviceLogEntries");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceLogEntries_DeviceId_TimestampUtc_Id",
                table: "DeviceLogEntries",
                columns: new[] { "DeviceId", "TimestampUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DeviceLogEntries_DeviceId_TimestampUtc_Id",
                table: "DeviceLogEntries");

            migrationBuilder.RenameColumn(
                name: "TimestampUtc",
                table: "DeviceLogEntries",
                newName: "ReceivedAtUtc");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeviceTimestampUtc",
                table: "DeviceLogEntries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DeviceUptimeMs",
                table: "DeviceLogEntries",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SequenceNumber",
                table: "DeviceLogEntries",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.Sql(
                """
                UPDATE "DeviceLogEntries"
                SET "SequenceNumber" = "Id";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceLogEntries_DeviceId_SequenceNumber",
                table: "DeviceLogEntries",
                columns: new[] { "DeviceId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceLogEntries_DeviceId_ReceivedAtUtc_Id",
                table: "DeviceLogEntries",
                columns: new[] { "DeviceId", "ReceivedAtUtc", "Id" });
        }
    }
}
