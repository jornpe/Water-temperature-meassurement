using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WaterTemperature.Api.Migrations
{
    /// <inheritdoc />
    public partial class ScaleDeviceLogStorageAndSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
            migrationBuilder.Sql(
                """
                UPDATE "DeviceLogEntries"
                SET "Level" = lower("Level")
                WHERE "Level" IS NOT NULL AND "Level" <> lower("Level");
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_DeviceLogEntries_DeviceId_Id"
                ON "DeviceLogEntries" ("DeviceId", "Id");
                """,
                suppressTransaction: true);

            migrationBuilder.Sql(
                """
                CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_DeviceLogEntries_DeviceId_ReceivedAtUtc_Id"
                ON "DeviceLogEntries" ("DeviceId", "ReceivedAtUtc", "Id");
                """,
                suppressTransaction: true);

            migrationBuilder.Sql(
                """
                CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_DeviceLogEntries_DeviceId_Level_Id"
                ON "DeviceLogEntries" ("DeviceId", "Level", "Id");
                """,
                suppressTransaction: true);

            migrationBuilder.Sql(
                """
                CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_DeviceLogEntries_Message_Trigram"
                ON "DeviceLogEntries" USING gin ("Message" gin_trgm_ops);
                """,
                suppressTransaction: true);

            migrationBuilder.DropIndex(
                name: "IX_DeviceLogEntries_DeviceId_ReceivedAtUtc",
                table: "DeviceLogEntries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """DROP INDEX CONCURRENTLY IF EXISTS "IX_DeviceLogEntries_Message_Trigram";""",
                suppressTransaction: true);

            migrationBuilder.Sql(
                """DROP INDEX CONCURRENTLY IF EXISTS "IX_DeviceLogEntries_DeviceId_Id";""",
                suppressTransaction: true);

            migrationBuilder.Sql(
                """DROP INDEX CONCURRENTLY IF EXISTS "IX_DeviceLogEntries_DeviceId_Level_Id";""",
                suppressTransaction: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceLogEntries_DeviceId_ReceivedAtUtc",
                table: "DeviceLogEntries",
                columns: new[] { "DeviceId", "ReceivedAtUtc" });

            migrationBuilder.Sql(
                """DROP INDEX CONCURRENTLY IF EXISTS "IX_DeviceLogEntries_DeviceId_ReceivedAtUtc_Id";""",
                suppressTransaction: true);
        }
    }
}
