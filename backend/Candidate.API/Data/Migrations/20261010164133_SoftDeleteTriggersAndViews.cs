using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Candidate.API.Data.Migrations
{
    /// <summary>
    /// Database objects EF Core does not model: triggers that block physical deletes, and views
    /// for anyone querying the database directly (support staff, DB Browser for SQLite, reports).
    /// </summary>
    /// <remarks>
    /// Views cannot take parameters, so they evaluate "today" with date('now', 'localtime'),
    /// the clock of the machine running the query. The API does not read these views; it passes
    /// the US Eastern business date as a query parameter. Treat the views as troubleshooting aids.
    /// </remarks>
    public partial class SoftDeleteTriggersAndViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Soft delete, last line of defence: a DELETE fails even when issued as raw SQL.
            migrationBuilder.Sql("""
                CREATE TRIGGER TR_Provider_BlockDelete
                BEFORE DELETE ON Provider
                BEGIN
                    SELECT RAISE(ABORT, 'Hard delete is not allowed on Provider. Set IsDeleted = 1 instead.');
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER TR_License_BlockDelete
                BEFORE DELETE ON License
                BEGIN
                    SELECT RAISE(ABORT, 'Hard delete is not allowed on License. Set IsDeleted = 1 instead.');
                END;
                """);

            // Standard view of providers: everything except soft-deleted rows.
            migrationBuilder.Sql("""
                CREATE VIEW vw_Providers AS
                SELECT p.ProviderId,
                       p.ProviderName,
                       c.Name AS County,
                       p.Status,
                       p.CreatedUtc,
                       p.CreatedBy,
                       p.ModifiedUtc,
                       p.ModifiedBy
                FROM Provider p
                JOIN County c ON c.CountyId = p.CountyId
                WHERE p.IsDeleted = 0;
                """);

            // Scenario 1. LEFT JOIN: an active provider with no valid license appears once, with NULL license columns.
            migrationBuilder.Sql("""
                CREATE VIEW vw_ActiveProvidersActiveLicenses AS
                SELECT p.ProviderId,
                       p.ProviderName,
                       p.County,
                       l.LicenseId,
                       l.LicenseNumber,
                       l.LicenseStatus,
                       l.ExpirationDate
                FROM vw_Providers p
                LEFT JOIN License l
                       ON l.ProviderId = p.ProviderId
                      AND l.IsDeleted = 0
                      AND l.LicenseStatus = 'Active'
                      AND l.ExpirationDate >= date('now', 'localtime')
                WHERE p.Status = 'Active';
                """);

            // Scenario 2. One row per expired license. Expired is decided by the date, not the stored status.
            migrationBuilder.Sql("""
                CREATE VIEW vw_ActiveProvidersExpiredLicenses AS
                SELECT p.ProviderId,
                       p.ProviderName,
                       p.County,
                       l.LicenseId,
                       l.LicenseNumber,
                       l.LicenseStatus AS StoredLicenseStatus,
                       l.ExpirationDate,
                       NOT EXISTS (
                           SELECT 1
                           FROM License v
                           WHERE v.ProviderId = p.ProviderId
                             AND v.IsDeleted = 0
                             AND v.LicenseStatus = 'Active'
                             AND v.ExpirationDate >= date('now', 'localtime')
                       ) AS HasNoValidLicense
                FROM vw_Providers p
                JOIN License l
                  ON l.ProviderId = p.ProviderId
                 AND l.IsDeleted = 0
                 AND l.ExpirationDate < date('now', 'localtime')
                WHERE p.Status = 'Active';
                """);

            // Audit and troubleshooting: the soft-deleted providers, with who deleted them and when.
            migrationBuilder.Sql("""
                CREATE VIEW vw_DeletedProvidersAudit AS
                SELECT p.ProviderId,
                       p.ProviderName,
                       c.Name AS County,
                       p.Status,
                       p.CreatedUtc,
                       p.CreatedBy,
                       p.DeletedUtc,
                       p.DeletedBy,
                       (SELECT COUNT(*) FROM License l WHERE l.ProviderId = p.ProviderId) AS LicenseCount
                FROM Provider p
                JOIN County c ON c.CountyId = p.CountyId
                WHERE p.IsDeleted = 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_DeletedProvidersAudit;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_ActiveProvidersExpiredLicenses;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_ActiveProvidersActiveLicenses;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS vw_Providers;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS TR_License_BlockDelete;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS TR_Provider_BlockDelete;");
        }
    }
}
