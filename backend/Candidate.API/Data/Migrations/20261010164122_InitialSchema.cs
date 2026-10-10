using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Candidate.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "County",
                columns: table => new
                {
                    CountyId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, collation: "NOCASE")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_County", x => x.CountyId);
                });

            migrationBuilder.CreateTable(
                name: "Provider",
                columns: table => new
                {
                    ProviderId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProviderName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false, collation: "NOCASE"),
                    CountyId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    DeletedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Provider", x => x.ProviderId);
                    table.CheckConstraint("CK_Provider_IsDeleted", "IsDeleted IN (0, 1)");
                    table.CheckConstraint("CK_Provider_ProviderName", "length(trim(ProviderName)) BETWEEN 1 AND 200");
                    table.CheckConstraint("CK_Provider_SoftDelete", "(IsDeleted = 0 AND DeletedUtc IS NULL) OR (IsDeleted = 1 AND DeletedUtc IS NOT NULL)");
                    table.CheckConstraint("CK_Provider_Status", "Status IN ('Active', 'Inactive', 'Pending')");
                    table.ForeignKey(
                        name: "FK_Provider_County_CountyId",
                        column: x => x.CountyId,
                        principalTable: "County",
                        principalColumn: "CountyId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "License",
                columns: table => new
                {
                    LicenseId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProviderId = table.Column<int>(type: "INTEGER", nullable: false),
                    LicenseNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, collation: "NOCASE"),
                    LicenseStatus = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    IssueDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    ExpirationDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    DeletedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeletedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_License", x => x.LicenseId);
                    table.CheckConstraint("CK_License_IsDeleted", "IsDeleted IN (0, 1)");
                    table.CheckConstraint("CK_License_LicenseNumber", "length(trim(LicenseNumber)) BETWEEN 1 AND 50");
                    table.CheckConstraint("CK_License_LicenseStatus", "LicenseStatus IN ('Active', 'Expired', 'Suspended')");
                    table.CheckConstraint("CK_License_SoftDelete", "(IsDeleted = 0 AND DeletedUtc IS NULL) OR (IsDeleted = 1 AND DeletedUtc IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_License_Provider_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Provider",
                        principalColumn: "ProviderId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "County",
                columns: new[] { "CountyId", "Name" },
                values: new object[,]
                {
                    { 1, "Appling" },
                    { 2, "Atkinson" },
                    { 3, "Bacon" },
                    { 4, "Baker" },
                    { 5, "Baldwin" },
                    { 6, "Banks" },
                    { 7, "Barrow" },
                    { 8, "Bartow" },
                    { 9, "Ben Hill" },
                    { 10, "Berrien" },
                    { 11, "Bibb" },
                    { 12, "Bleckley" },
                    { 13, "Brantley" },
                    { 14, "Brooks" },
                    { 15, "Bryan" },
                    { 16, "Bulloch" },
                    { 17, "Burke" },
                    { 18, "Butts" },
                    { 19, "Calhoun" },
                    { 20, "Camden" },
                    { 21, "Candler" },
                    { 22, "Carroll" },
                    { 23, "Catoosa" },
                    { 24, "Charlton" },
                    { 25, "Chatham" },
                    { 26, "Chattahoochee" },
                    { 27, "Chattooga" },
                    { 28, "Cherokee" },
                    { 29, "Clarke" },
                    { 30, "Clay" },
                    { 31, "Clayton" },
                    { 32, "Clinch" },
                    { 33, "Cobb" },
                    { 34, "Coffee" },
                    { 35, "Colquitt" },
                    { 36, "Columbia" },
                    { 37, "Cook" },
                    { 38, "Coweta" },
                    { 39, "Crawford" },
                    { 40, "Crisp" },
                    { 41, "Dade" },
                    { 42, "Dawson" },
                    { 43, "Decatur" },
                    { 44, "DeKalb" },
                    { 45, "Dodge" },
                    { 46, "Dooly" },
                    { 47, "Dougherty" },
                    { 48, "Douglas" },
                    { 49, "Early" },
                    { 50, "Echols" },
                    { 51, "Effingham" },
                    { 52, "Elbert" },
                    { 53, "Emanuel" },
                    { 54, "Evans" },
                    { 55, "Fannin" },
                    { 56, "Fayette" },
                    { 57, "Floyd" },
                    { 58, "Forsyth" },
                    { 59, "Franklin" },
                    { 60, "Fulton" },
                    { 61, "Gilmer" },
                    { 62, "Glascock" },
                    { 63, "Glynn" },
                    { 64, "Gordon" },
                    { 65, "Grady" },
                    { 66, "Greene" },
                    { 67, "Gwinnett" },
                    { 68, "Habersham" },
                    { 69, "Hall" },
                    { 70, "Hancock" },
                    { 71, "Haralson" },
                    { 72, "Harris" },
                    { 73, "Hart" },
                    { 74, "Heard" },
                    { 75, "Henry" },
                    { 76, "Houston" },
                    { 77, "Irwin" },
                    { 78, "Jackson" },
                    { 79, "Jasper" },
                    { 80, "Jeff Davis" },
                    { 81, "Jefferson" },
                    { 82, "Jenkins" },
                    { 83, "Johnson" },
                    { 84, "Jones" },
                    { 85, "Lamar" },
                    { 86, "Lanier" },
                    { 87, "Laurens" },
                    { 88, "Lee" },
                    { 89, "Liberty" },
                    { 90, "Lincoln" },
                    { 91, "Long" },
                    { 92, "Lowndes" },
                    { 93, "Lumpkin" },
                    { 94, "Macon" },
                    { 95, "Madison" },
                    { 96, "Marion" },
                    { 97, "McDuffie" },
                    { 98, "McIntosh" },
                    { 99, "Meriwether" },
                    { 100, "Miller" },
                    { 101, "Mitchell" },
                    { 102, "Monroe" },
                    { 103, "Montgomery" },
                    { 104, "Morgan" },
                    { 105, "Murray" },
                    { 106, "Muscogee" },
                    { 107, "Newton" },
                    { 108, "Oconee" },
                    { 109, "Oglethorpe" },
                    { 110, "Paulding" },
                    { 111, "Peach" },
                    { 112, "Pickens" },
                    { 113, "Pierce" },
                    { 114, "Pike" },
                    { 115, "Polk" },
                    { 116, "Pulaski" },
                    { 117, "Putnam" },
                    { 118, "Quitman" },
                    { 119, "Rabun" },
                    { 120, "Randolph" },
                    { 121, "Richmond" },
                    { 122, "Rockdale" },
                    { 123, "Schley" },
                    { 124, "Screven" },
                    { 125, "Seminole" },
                    { 126, "Spalding" },
                    { 127, "Stephens" },
                    { 128, "Stewart" },
                    { 129, "Sumter" },
                    { 130, "Talbot" },
                    { 131, "Taliaferro" },
                    { 132, "Tattnall" },
                    { 133, "Taylor" },
                    { 134, "Telfair" },
                    { 135, "Terrell" },
                    { 136, "Thomas" },
                    { 137, "Tift" },
                    { 138, "Toombs" },
                    { 139, "Towns" },
                    { 140, "Treutlen" },
                    { 141, "Troup" },
                    { 142, "Turner" },
                    { 143, "Twiggs" },
                    { 144, "Union" },
                    { 145, "Upson" },
                    { 146, "Walker" },
                    { 147, "Walton" },
                    { 148, "Ware" },
                    { 149, "Warren" },
                    { 150, "Washington" },
                    { 151, "Wayne" },
                    { 152, "Webster" },
                    { 153, "Wheeler" },
                    { 154, "White" },
                    { 155, "Whitfield" },
                    { 156, "Wilcox" },
                    { 157, "Wilkes" },
                    { 158, "Wilkinson" },
                    { 159, "Worth" }
                });

            migrationBuilder.CreateIndex(
                name: "UX_County_Name",
                table: "County",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_License_Provider_Expiration",
                table: "License",
                columns: new[] { "ProviderId", "ExpirationDate" },
                filter: "IsDeleted = 0");

            migrationBuilder.CreateIndex(
                name: "UX_License_Provider_Number",
                table: "License",
                columns: new[] { "ProviderId", "LicenseNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Provider_CountyId",
                table: "Provider",
                column: "CountyId");

            migrationBuilder.CreateIndex(
                name: "IX_Provider_Name",
                table: "Provider",
                column: "ProviderName",
                filter: "IsDeleted = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Provider_Status_County",
                table: "Provider",
                columns: new[] { "Status", "CountyId" },
                filter: "IsDeleted = 0");

            migrationBuilder.CreateIndex(
                name: "UX_Provider_Name_County",
                table: "Provider",
                columns: new[] { "ProviderName", "CountyId" },
                unique: true,
                filter: "IsDeleted = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "License");

            migrationBuilder.DropTable(
                name: "Provider");

            migrationBuilder.DropTable(
                name: "County");
        }
    }
}
