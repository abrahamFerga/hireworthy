using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hireworthy.Hiring.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ScreeningWithCitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "screening_results",
                schema: "hiring",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RubricId = table.Column<Guid>(type: "uuid", nullable: false),
                    RubricVersion = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ScreenedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TotalScore = table.Column<int>(type: "integer", nullable: false),
                    MaxScore = table.Column<int>(type: "integer", nullable: false),
                    UnresolvedCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_screening_results", x => x.Id);
                    table.ForeignKey(
                        name: "FK_screening_results_applicants_ApplicantId",
                        column: x => x.ApplicantId,
                        principalSchema: "hiring",
                        principalTable: "applicants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_screening_results_rubrics_RubricId",
                        column: x => x.RubricId,
                        principalSchema: "hiring",
                        principalTable: "rubrics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "criterion_scores",
                schema: "hiring",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScreeningResultId = table.Column<Guid>(type: "uuid", nullable: false),
                    RubricCriterionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CriterionName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    Unresolved = table.Column<bool>(type: "boolean", nullable: false),
                    CitationText = table.Column<string>(type: "text", nullable: true),
                    CitationStart = table.Column<int>(type: "integer", nullable: false),
                    CitationEnd = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_criterion_scores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_criterion_scores_screening_results_ScreeningResultId",
                        column: x => x.ScreeningResultId,
                        principalSchema: "hiring",
                        principalTable: "screening_results",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_criterion_scores_ScreeningResultId",
                schema: "hiring",
                table: "criterion_scores",
                column: "ScreeningResultId");

            migrationBuilder.CreateIndex(
                name: "IX_screening_results_ApplicantId",
                schema: "hiring",
                table: "screening_results",
                column: "ApplicantId");

            migrationBuilder.CreateIndex(
                name: "IX_screening_results_RubricId",
                schema: "hiring",
                table: "screening_results",
                column: "RubricId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "criterion_scores",
                schema: "hiring");

            migrationBuilder.DropTable(
                name: "screening_results",
                schema: "hiring");
        }
    }
}
