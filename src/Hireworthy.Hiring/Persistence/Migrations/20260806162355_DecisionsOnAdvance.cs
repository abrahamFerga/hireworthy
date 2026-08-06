using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hireworthy.Hiring.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DecisionsOnAdvance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "decisions",
                schema: "hiring",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FromStage = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ToStage = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Reason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ScreeningResultId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_decisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_decisions_applicants_ApplicantId",
                        column: x => x.ApplicantId,
                        principalSchema: "hiring",
                        principalTable: "applicants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_decisions_screening_results_ScreeningResultId",
                        column: x => x.ScreeningResultId,
                        principalSchema: "hiring",
                        principalTable: "screening_results",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_decisions_ApplicantId",
                schema: "hiring",
                table: "decisions",
                column: "ApplicantId");

            migrationBuilder.CreateIndex(
                name: "IX_decisions_ScreeningResultId",
                schema: "hiring",
                table: "decisions",
                column: "ScreeningResultId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "decisions",
                schema: "hiring");
        }
    }
}
