using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinancialPortfolioAPI.Infrastructure.Data.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class InitialSqlite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EntityName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    EntityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    BeforeJson = table.Column<string>(type: "TEXT", nullable: true),
                    AfterJson = table.Column<string>(type: "TEXT", nullable: false),
                    Actor = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IpAddress = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Timestamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Changes",
                columns: table => new
                {
                    Sequence = table.Column<long>(type: "INTEGER", nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    EntityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false),
                    CommittedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Changes", x => x.Sequence);
                });

            migrationBuilder.CreateTable(
                name: "Portfolios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ClientId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ClientName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    CashBalance = table.Column<decimal>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    RiskProfile = table.Column<int>(type: "INTEGER", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastUpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Portfolios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PriceHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StoreStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    Revision = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Holdings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PortfolioId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CompanyName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    AssetClass = table.Column<int>(type: "INTEGER", nullable: false),
                    Shares = table.Column<decimal>(type: "TEXT", nullable: false),
                    CurrentPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    CostBasis = table.Column<decimal>(type: "TEXT", nullable: false),
                    LastPriceUpdate = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Holdings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Holdings_Portfolios_PortfolioId",
                        column: x => x.PortfolioId,
                        principalTable: "Portfolios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PortfolioId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Shares = table.Column<decimal>(type: "TEXT", nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    Commission = table.Column<decimal>(type: "TEXT", nullable: false),
                    NetAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    TransactionDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    SettlementDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Transactions_Portfolios_PortfolioId",
                        column: x => x.PortfolioId,
                        principalTable: "Portfolios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "StoreStates",
                columns: new[] { "Id", "Revision" },
                values: new object[] { 1, 0L });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityId_Timestamp",
                table: "AuditLogs",
                columns: new[] { "EntityId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_Changes_EntityId_Version",
                table: "Changes",
                columns: new[] { "EntityId", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_Holdings_PortfolioId_Symbol",
                table: "Holdings",
                columns: new[] { "PortfolioId", "Symbol" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Portfolios_ClientId",
                table: "Portfolios",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Portfolios_RiskProfile",
                table: "Portfolios",
                column: "RiskProfile");

            migrationBuilder.CreateIndex(
                name: "IX_Portfolios_Status",
                table: "Portfolios",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PriceHistories_Symbol_RecordedAt",
                table: "PriceHistories",
                columns: new[] { "Symbol", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_PortfolioId_ReferenceNumber",
                table: "Transactions",
                columns: new[] { "PortfolioId", "ReferenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_Status",
                table: "Transactions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TransactionDate",
                table: "Transactions",
                column: "TransactionDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "Changes");

            migrationBuilder.DropTable(
                name: "Holdings");

            migrationBuilder.DropTable(
                name: "PriceHistories");

            migrationBuilder.DropTable(
                name: "StoreStates");

            migrationBuilder.DropTable(
                name: "Transactions");

            migrationBuilder.DropTable(
                name: "Portfolios");
        }
    }
}
