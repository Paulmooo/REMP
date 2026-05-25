using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Remp.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class FixUserAgentRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Agents_AgentId",
                table: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "RoleUser");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_AgentId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "AgentId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "RoleName",
                table: "AspNetRoles");

            migrationBuilder.AddForeignKey(
                name: "FK_Agents_AspNetUsers_Id",
                table: "Agents",
                column: "Id",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PhotographyCompanies_AspNetUsers_Id",
                table: "PhotographyCompanies",
                column: "Id",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Agents_AspNetUsers_Id",
                table: "Agents");

            migrationBuilder.DropForeignKey(
                name: "FK_PhotographyCompanies_AspNetUsers_Id",
                table: "PhotographyCompanies");

            migrationBuilder.AddColumn<string>(
                name: "AgentId",
                table: "AspNetUsers",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RoleName",
                table: "AspNetRoles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RoleUser",
                columns: table => new
                {
                    RolesId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UsersId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleUser", x => new { x.RolesId, x.UsersId });
                    table.ForeignKey(
                        name: "FK_RoleUser_AspNetRoles_RolesId",
                        column: x => x.RolesId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoleUser_AspNetUsers_UsersId",
                        column: x => x.UsersId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_AgentId",
                table: "AspNetUsers",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_RoleUser_UsersId",
                table: "RoleUser",
                column: "UsersId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Agents_AgentId",
                table: "AspNetUsers",
                column: "AgentId",
                principalTable: "Agents",
                principalColumn: "Id");
        }
    }
}
