using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Krypteia.Samples.WebApi.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KrypteiaResetTokens",
                columns: table => new
                {
                    TokenHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KrypteiaResetTokens", x => x.TokenHash);
                });

            migrationBuilder.CreateTable(
                name: "KrypteiaUserKeys",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    PublicKey = table.Column<string>(type: "TEXT", nullable: false),
                    EncryptedPrivateKeyBackup = table.Column<string>(type: "TEXT", nullable: false),
                    KeyVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    MasterKeyId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KrypteiaUserKeys", x => x.UserId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KrypteiaResetTokens_UserId_CreatedAt",
                table: "KrypteiaResetTokens",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_KrypteiaUserKeys_MasterKeyId",
                table: "KrypteiaUserKeys",
                column: "MasterKeyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KrypteiaResetTokens");

            migrationBuilder.DropTable(
                name: "KrypteiaUserKeys");
        }
    }
}
