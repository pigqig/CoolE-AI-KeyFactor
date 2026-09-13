using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KeyFactorDashboard.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class PlantEdition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_event",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    At = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Action = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    EntityId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    DetailJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_event", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "model_version",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    DatasetId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    VersionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Algorithm = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    MetricsJson = table.Column<string>(type: "TEXT", nullable: false),
                    ImportancesJson = table.Column<string>(type: "TEXT", nullable: true),
                    Artifact = table.Column<byte[]>(type: "BLOB", nullable: true),
                    TrainedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                    TrainedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    ApprovedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_model_version", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "plant_role",
                columns: table => new
                {
                    Name = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plant_role", x => x.Name);
                });

            migrationBuilder.CreateTable(
                name: "plant_user",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plant_user", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "process_dataset",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    RowCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ColumnsJson = table.Column<string>(type: "TEXT", nullable: false),
                    PreviewJson = table.Column<string>(type: "TEXT", nullable: false),
                    OverviewJson = table.Column<string>(type: "TEXT", nullable: true),
                    PayloadKind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Payload = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_process_dataset", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "whatif_note",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ModelId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    RequestJson = table.Column<string>(type: "TEXT", nullable: false),
                    ResponseJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_whatif_note", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "train_job",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    DatasetId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    TargetColumn = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Algorithm = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Task = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    MetricsJson = table.Column<string>(type: "TEXT", nullable: false),
                    FeatureNamesJson = table.Column<string>(type: "TEXT", nullable: false),
                    ImportancesJson = table.Column<string>(type: "TEXT", nullable: true),
                    Artifact = table.Column<byte[]>(type: "BLOB", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_train_job", x => x.Id);
                    table.ForeignKey(
                        name: "FK_train_job_process_dataset_DatasetId",
                        column: x => x.DatasetId,
                        principalTable: "process_dataset",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_model_version_DatasetId_VersionNumber",
                table: "model_version",
                columns: new[] { "DatasetId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_plant_user_Username",
                table: "plant_user",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_train_job_DatasetId",
                table: "train_job",
                column: "DatasetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_event");

            migrationBuilder.DropTable(
                name: "model_version");

            migrationBuilder.DropTable(
                name: "plant_role");

            migrationBuilder.DropTable(
                name: "plant_user");

            migrationBuilder.DropTable(
                name: "train_job");

            migrationBuilder.DropTable(
                name: "whatif_note");

            migrationBuilder.DropTable(
                name: "process_dataset");
        }
    }
}
