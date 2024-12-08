using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RedisCache.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "T_Author",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Age = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RedisKey = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_T_Author", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "T_Book",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BookName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PublishTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RedisKey = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_T_Book", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "T_TestEntity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RedisKey = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_T_TestEntity", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "T_BookAuthors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuthorsId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BookrsId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_T_BookAuthors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_T_BookAuthors_T_Author_AuthorsId",
                        column: x => x.AuthorsId,
                        principalTable: "T_Author",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_T_BookAuthors_T_Book_BookrsId",
                        column: x => x.BookrsId,
                        principalTable: "T_Book",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_T_BookAuthors_AuthorsId",
                table: "T_BookAuthors",
                column: "AuthorsId");

            migrationBuilder.CreateIndex(
                name: "IX_T_BookAuthors_BookrsId",
                table: "T_BookAuthors",
                column: "BookrsId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "T_BookAuthors");

            migrationBuilder.DropTable(
                name: "T_TestEntity");

            migrationBuilder.DropTable(
                name: "T_Author");

            migrationBuilder.DropTable(
                name: "T_Book");
        }
    }
}
