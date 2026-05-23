using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartEdu.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBenchmarkTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BenchmarkResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ModelName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChunkStrategy = table.Column<int>(type: "int", nullable: false),
                    EmbeddingModel = table.Column<int>(type: "int", nullable: true),
                    Precision = table.Column<double>(type: "float", nullable: false),
                    Recall = table.Column<double>(type: "float", nullable: false),
                    LatencyMs = table.Column<long>(type: "bigint", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BenchmarkResults", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BenchmarkResults");
        }
    }
}
