using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YemenGuide.Data.Migrations
{
    public partial class InitialCreateWithThemeColor : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IconClass",
                table: "Services",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ThemeColor",
                table: "Services",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "PlaceId",
                table: "Reports",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CityID",
                table: "Places",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Cities",
                columns: table => new
                {
                    CityID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CityName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ThemeColor = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cities", x => x.CityID);
                });

            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SiteName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SiteDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LogoPath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FaviconPath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AutoApprovePlaces = table.Column<bool>(type: "bit", nullable: false),
                    AllowNewRegistrations = table.Column<bool>(type: "bit", nullable: false),
                    ContactEmail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SupportPhone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FacebookLink = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TwitterLink = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InstagramLink = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WhatsAppNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MaintenanceMode = table.Column<bool>(type: "bit", nullable: false),
                    MaintenanceMessage = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Reports_PlaceId",
                table: "Reports",
                column: "PlaceId");

            migrationBuilder.CreateIndex(
                name: "IX_Places_CityID",
                table: "Places",
                column: "CityID");

            migrationBuilder.CreateIndex(
                name: "IX_Cities_CityName",
                table: "Cities",
                column: "CityName",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Places_Cities",
                table: "Places",
                column: "CityID",
                principalTable: "Cities",
                principalColumn: "CityID");

            migrationBuilder.AddForeignKey(
                name: "FK_Reports_Places_PlaceId",
                table: "Reports",
                column: "PlaceId",
                principalTable: "Places",
                principalColumn: "PlaceID");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Places_Cities",
                table: "Places");

            migrationBuilder.DropForeignKey(
                name: "FK_Reports_Places_PlaceId",
                table: "Reports");

            migrationBuilder.DropTable(
                name: "Cities");

            migrationBuilder.DropTable(
                name: "SystemSettings");

            migrationBuilder.DropIndex(
                name: "IX_Reports_PlaceId",
                table: "Reports");

            migrationBuilder.DropIndex(
                name: "IX_Places_CityID",
                table: "Places");

            migrationBuilder.DropColumn(
                name: "IconClass",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "ThemeColor",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "PlaceId",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "CityID",
                table: "Places");
        }
    }
}
