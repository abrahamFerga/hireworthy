using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hireworthy.Hiring.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialHiringSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "hiring");

            migrationBuilder.CreateTable(
                name: "requisitions",
                schema: "hiring",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    JobDescription = table.Column<string>(type: "character varying(32000)", maxLength: 32000, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Location = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    HiringManager = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_requisitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "rubrics",
                schema: "hiring",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequisitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Rationale = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ApprovedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rubrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rubrics_requisitions_RequisitionId",
                        column: x => x.RequisitionId,
                        principalSchema: "hiring",
                        principalTable: "requisitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rubric_criteria",
                schema: "hiring",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RubricId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Requirement = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Weight = table.Column<int>(type: "integer", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rubric_criteria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rubric_criteria_rubrics_RubricId",
                        column: x => x.RubricId,
                        principalSchema: "hiring",
                        principalTable: "rubrics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_requisitions_TenantId_Reference",
                schema: "hiring",
                table: "requisitions",
                columns: new[] { "TenantId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rubric_criteria_RubricId",
                schema: "hiring",
                table: "rubric_criteria",
                column: "RubricId");

            migrationBuilder.CreateIndex(
                name: "IX_rubrics_RequisitionId_Version",
                schema: "hiring",
                table: "rubrics",
                columns: new[] { "RequisitionId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rubric_criteria",
                schema: "hiring");

            migrationBuilder.DropTable(
                name: "rubrics",
                schema: "hiring");

            migrationBuilder.DropTable(
                name: "requisitions",
                schema: "hiring");
        }
    }
}
