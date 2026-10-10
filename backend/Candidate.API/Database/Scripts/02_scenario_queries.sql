-- 02_scenario_queries.sql
-- The required data scenarios in plain SQL. Read-only: every statement is a SELECT.
-- Open Database/Data/providers.db in DB Browser for SQLite (or the sqlite3 shell) and run any block.
--
-- Rules used throughout (the same rules the API applies in Domain/LicenseRules.cs):
--   * A soft-deleted row has IsDeleted = 1. Standard queries always filter IsDeleted = 0.
--   * A license is EXPIRED when ExpirationDate is before today, whatever LicenseStatus says.
--   * A license is VALID when LicenseStatus = 'Active' and ExpirationDate is today or later.
--   * Dates are stored as ISO text (yyyy-MM-dd), so comparing the text compares the dates.
--
-- "Today" here is date('now', 'localtime'): the date on the machine running the query.
-- The API instead passes the US Eastern business date as a parameter.


-- ---------------------------------------------------------------------------------------
-- Scenario 1: active providers and their active (currently valid) licenses.
-- LEFT JOIN keeps an active provider that has no valid license; its license columns are NULL.
-- ---------------------------------------------------------------------------------------
SELECT p.ProviderId,
       p.ProviderName,
       c.Name AS County,
       l.LicenseNumber,
       l.ExpirationDate
FROM Provider p
JOIN County c ON c.CountyId = p.CountyId
LEFT JOIN License l
       ON l.ProviderId = p.ProviderId
      AND l.IsDeleted = 0
      AND l.LicenseStatus = 'Active'
      AND l.ExpirationDate >= date('now', 'localtime')
WHERE p.IsDeleted = 0
  AND p.Status = 'Active'
ORDER BY p.ProviderName, l.ExpirationDate;

-- The same result from the view:
SELECT * FROM vw_ActiveProvidersActiveLicenses ORDER BY ProviderName, ExpirationDate;


-- ---------------------------------------------------------------------------------------
-- Scenario 2: providers that appear active but have expired licenses.
-- StoredLicenseStatus is shown beside the date on purpose: a row that still says 'Active'
-- with a past ExpirationDate is the case the provider's status alone would hide.
-- HasNoValidLicense = 1 marks the providers with nothing valid left at all.
-- ---------------------------------------------------------------------------------------
SELECT p.ProviderId,
       p.ProviderName,
       c.Name AS County,
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
FROM Provider p
JOIN County c ON c.CountyId = p.CountyId
JOIN License l
  ON l.ProviderId = p.ProviderId
 AND l.IsDeleted = 0
 AND l.ExpirationDate < date('now', 'localtime')
WHERE p.IsDeleted = 0
  AND p.Status = 'Active'
ORDER BY p.ProviderName, l.ExpirationDate;

-- The same result from the view:
SELECT * FROM vw_ActiveProvidersExpiredLicenses ORDER BY ProviderName, ExpirationDate;


-- ---------------------------------------------------------------------------------------
-- Scenario 3: soft-deleted providers are excluded from standard results.
-- ---------------------------------------------------------------------------------------

-- Standard listing (what the application shows):
SELECT * FROM vw_Providers ORDER BY ProviderName;

-- Proof that the deleted rows are still in the table, just filtered out:
SELECT (SELECT COUNT(*) FROM Provider)                     AS RowsInTable,
       (SELECT COUNT(*) FROM Provider WHERE IsDeleted = 0) AS ShownInStandardResults,
       (SELECT COUNT(*) FROM Provider WHERE IsDeleted = 1) AS SoftDeleted;

-- Audit and troubleshooting: who deleted what, and when.
SELECT * FROM vw_DeletedProvidersAudit ORDER BY DeletedUtc DESC;

-- Licenses that belong to a soft-deleted provider. They keep IsDeleted = 0 themselves;
-- they are hidden because their provider is, so restoring the provider brings them back.
SELECT l.LicenseId, l.LicenseNumber, l.LicenseStatus, l.ExpirationDate, p.ProviderName, p.DeletedUtc
FROM License l
JOIN Provider p ON p.ProviderId = l.ProviderId
WHERE p.IsDeleted = 1;


-- ---------------------------------------------------------------------------------------
-- Hard delete is blocked by trigger. This statement is commented out because it fails by
-- design with: "Hard delete is not allowed on Provider. Set IsDeleted = 1 instead."
-- ---------------------------------------------------------------------------------------
-- DELETE FROM Provider WHERE ProviderId = 1;
