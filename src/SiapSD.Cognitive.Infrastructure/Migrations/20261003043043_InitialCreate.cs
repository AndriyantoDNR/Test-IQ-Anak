using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SiapSD.Cognitive.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssessmentBlueprints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssessmentBlueprints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AssessmentDomainScores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssessmentSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CognitiveDomainId = table.Column<Guid>(type: "uuid", nullable: false),
                    RawScore = table.Column<int>(type: "integer", nullable: false),
                    InternalScore = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    MetricsJson = table.Column<string>(type: "text", nullable: false),
                    InterpretationKey = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssessmentDomainScores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AssessmentSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssessmentBlueprintId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AgeInMonthsAtAssessment = table.Column<int>(type: "integer", nullable: false),
                    CurrentSubtestId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrentQuestionIndex = table.Column<int>(type: "integer", nullable: false),
                    TotalDurationMs = table.Column<long>(type: "bigint", nullable: false),
                    PreAssessmentContextJson = table.Column<string>(type: "text", nullable: true),
                    PostAssessmentContextJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssessmentSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Children",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Nickname = table.Column<string>(type: "text", nullable: true),
                    BirthDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Gender = table.Column<int>(type: "integer", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Children", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveDomains",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveDomains", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Questions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    CognitiveDomainId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubtestId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionType = table.Column<int>(type: "integer", nullable: false),
                    Difficulty = table.Column<int>(type: "integer", nullable: false),
                    AgeMinMonths = table.Column<int>(type: "integer", nullable: false),
                    AgeMaxMonths = table.Column<int>(type: "integer", nullable: false),
                    Instruction = table.Column<string>(type: "text", nullable: false),
                    QuestionText = table.Column<string>(type: "text", nullable: false),
                    StimulusJson = table.Column<string>(type: "text", nullable: true),
                    CorrectAnswerJson = table.Column<string>(type: "text", nullable: false),
                    Explanation = table.Column<string>(type: "text", nullable: true),
                    IsAnswerRandomized = table.Column<bool>(type: "boolean", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Questions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AssessmentBlueprintItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssessmentBlueprintId = table.Column<Guid>(type: "uuid", nullable: false),
                    CognitiveDomainId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubtestId = table.Column<Guid>(type: "uuid", nullable: true),
                    InitialDifficulty = table.Column<int>(type: "integer", nullable: false),
                    MinimumQuestions = table.Column<int>(type: "integer", nullable: false),
                    MaximumQuestions = table.Column<int>(type: "integer", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsAdaptive = table.Column<bool>(type: "boolean", nullable: false),
                    BasalRuleJson = table.Column<string>(type: "text", nullable: true),
                    CeilingRuleJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssessmentBlueprintItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssessmentBlueprintItems_AssessmentBlueprints_AssessmentBlu~",
                        column: x => x.AssessmentBlueprintId,
                        principalTable: "AssessmentBlueprints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssessmentResponses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssessmentSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionVersion = table.Column<int>(type: "integer", nullable: false),
                    QuestionSnapshotJson = table.Column<string>(type: "text", nullable: false),
                    CognitiveDomainId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubtestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Difficulty = table.Column<int>(type: "integer", nullable: false),
                    PresentedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FirstInteractionAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AnsweredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResponseTimeMs = table.Column<long>(type: "bigint", nullable: true),
                    FirstAnswerJson = table.Column<string>(type: "text", nullable: true),
                    FinalAnswerJson = table.Column<string>(type: "text", nullable: false),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    ChangedAnswer = table.Column<bool>(type: "boolean", nullable: false),
                    HintUsed = table.Column<bool>(type: "boolean", nullable: false),
                    Skipped = table.Column<bool>(type: "boolean", nullable: false),
                    TimedOut = table.Column<bool>(type: "boolean", nullable: false),
                    QuestionOrder = table.Column<int>(type: "integer", nullable: false),
                    SessionElapsedMs = table.Column<long>(type: "bigint", nullable: false),
                    ResponseTelemetryJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssessmentResponses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssessmentResponses_AssessmentSessions_AssessmentSessionId",
                        column: x => x.AssessmentSessionId,
                        principalTable: "AssessmentSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Subtests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CognitiveDomainId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subtests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subtests_CognitiveDomains_CognitiveDomainId",
                        column: x => x.CognitiveDomainId,
                        principalTable: "CognitiveDomains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuestionOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
                    Value = table.Column<string>(type: "text", nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionOptions_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentBlueprintItems_AssessmentBlueprintId",
                table: "AssessmentBlueprintItems",
                column: "AssessmentBlueprintId");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentResponses_AssessmentSessionId_QuestionId",
                table: "AssessmentResponses",
                columns: new[] { "AssessmentSessionId", "QuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveDomains_Code",
                table: "CognitiveDomains",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuestionOptions_QuestionId",
                table: "QuestionOptions",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_Questions_Code",
                table: "Questions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subtests_CognitiveDomainId_Code",
                table: "Subtests",
                columns: new[] { "CognitiveDomainId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssessmentBlueprintItems");

            migrationBuilder.DropTable(
                name: "AssessmentDomainScores");

            migrationBuilder.DropTable(
                name: "AssessmentResponses");

            migrationBuilder.DropTable(
                name: "Children");

            migrationBuilder.DropTable(
                name: "QuestionOptions");

            migrationBuilder.DropTable(
                name: "Subtests");

            migrationBuilder.DropTable(
                name: "AssessmentBlueprints");

            migrationBuilder.DropTable(
                name: "AssessmentSessions");

            migrationBuilder.DropTable(
                name: "Questions");

            migrationBuilder.DropTable(
                name: "CognitiveDomains");
        }
    }
}
