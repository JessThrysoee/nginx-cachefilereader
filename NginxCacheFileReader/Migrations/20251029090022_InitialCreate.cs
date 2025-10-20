using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NginxCacheFileReader.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cache_items",
                columns: table => new
                {
                    cache_item_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    key = table.Column<string>(type: "TEXT", nullable: false),
                    status_code = table.Column<int>(type: "INTEGER", nullable: false),
                    path = table.Column<string>(type: "TEXT", nullable: false),
                    body_file_signature_hex = table.Column<string>(type: "TEXT", nullable: true),
                    body_file_signature_ascii = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cache_items", x => x.cache_item_id);
                });

            migrationBuilder.CreateTable(
                name: "cache_item_header",
                columns: table => new
                {
                    cache_item_header_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    version = table.Column<int>(type: "INTEGER", nullable: false),
                    valid_sec = table.Column<DateTime>(type: "TEXT", nullable: true),
                    updating_sec = table.Column<DateTime>(type: "TEXT", nullable: true),
                    error_sec = table.Column<DateTime>(type: "TEXT", nullable: true),
                    last_modified = table.Column<DateTime>(type: "TEXT", nullable: true),
                    date = table.Column<DateTime>(type: "TEXT", nullable: true),
                    crc32 = table.Column<int>(type: "INTEGER", nullable: true),
                    valid_msec = table.Column<long>(type: "INTEGER", nullable: true),
                    etag = table.Column<string>(type: "TEXT", nullable: true),
                    vary = table.Column<string>(type: "TEXT", nullable: true),
                    variant = table.Column<string>(type: "TEXT", nullable: true),
                    cache_item_id = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cache_item_header", x => x.cache_item_header_id);
                    table.ForeignKey(
                        name: "fk_cache_item_header_cache_items_cache_item_id",
                        column: x => x.cache_item_id,
                        principalTable: "cache_items",
                        principalColumn: "cache_item_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "http_header",
                columns: table => new
                {
                    http_header_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    value = table.Column<string>(type: "TEXT", nullable: false),
                    cache_item_id = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_http_header", x => x.http_header_id);
                    table.ForeignKey(
                        name: "fk_http_header_cache_items_cache_item_id",
                        column: x => x.cache_item_id,
                        principalTable: "cache_items",
                        principalColumn: "cache_item_id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cache_item_header");

            migrationBuilder.DropTable(
                name: "http_header");

            migrationBuilder.DropTable(
                name: "cache_items");
        }
    }
}
