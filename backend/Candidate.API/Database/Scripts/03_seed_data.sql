-- 03_seed_data.sql
-- Sample data that demonstrates every required scenario.
--
-- Run after 01_schema.sql (the County lookup rows are part of the schema script).
-- The API loads this same file at startup in Development when the Provider table is empty,
-- and `dotnet run --project backend/Candidate.API -- --init-db` does the same on demand.
--
-- Repeatable: rows have fixed primary keys and use INSERT OR IGNORE, so running the
-- script a second time changes nothing.
--
-- Dates are deliberately far in the past (2024-2025) or far in the future (2030+), so the
-- scenarios do not drift as the calendar moves on.
--
--  Id  Provider                          Status    Demonstrates
--  --  --------------------------------  --------  -------------------------------------------------
--   1  Peachtree Early Learning Center   Active    All licenses valid
--   2  Decatur Family Child Care         Active    One expired license, one valid
--   3  Savannah Coastal Kids Academy     Active    Stored status says Active but the date has passed
--   4  Marietta Montessori House         Active    Only a suspended license (no valid license)
--   5  Athens Bright Beginnings          Active    No licenses at all
--   6  Augusta Riverwalk Preschool       Inactive  Inactive provider, expired license
--   7  Macon Little Scholars             Pending   Pending provider, nothing issued yet
--   8  Columbus Sunshine Daycare         Active    One valid license, one expired by date
--   9  Gwinnett Growing Minds            Active    Single valid license
--  10  Valdosta Southside Learning       Inactive  Inactive provider that still holds a valid license
--  11  Rome Hilltop Child Care           Active    SOFT-DELETED, with a license
--  12  Albany Tiny Tots                  Inactive  SOFT-DELETED, no licenses
--  13  Rome Hilltop Child Care           Pending   Re-created after #11 was deleted (same name and county)

INSERT OR IGNORE INTO Provider
    (ProviderId, ProviderName, CountyId, Status, CreatedUtc, CreatedBy, ModifiedUtc, ModifiedBy, IsDeleted, DeletedUtc, DeletedBy, ConcurrencyStamp)
VALUES
    (1,  'Peachtree Early Learning Center', (SELECT CountyId FROM County WHERE Name = 'Fulton'),    'Active',   '2024-01-15 14:00:00', 'seed', NULL, NULL, 0, NULL, NULL, 'seed-provider-01'),
    (2,  'Decatur Family Child Care',       (SELECT CountyId FROM County WHERE Name = 'DeKalb'),    'Active',   '2024-02-01 15:30:00', 'seed', NULL, NULL, 0, NULL, NULL, 'seed-provider-02'),
    (3,  'Savannah Coastal Kids Academy',   (SELECT CountyId FROM County WHERE Name = 'Chatham'),   'Active',   '2024-02-20 13:10:00', 'seed', NULL, NULL, 0, NULL, NULL, 'seed-provider-03'),
    (4,  'Marietta Montessori House',       (SELECT CountyId FROM County WHERE Name = 'Cobb'),      'Active',   '2024-03-05 16:45:00', 'seed', NULL, NULL, 0, NULL, NULL, 'seed-provider-04'),
    (5,  'Athens Bright Beginnings',        (SELECT CountyId FROM County WHERE Name = 'Clarke'),    'Active',   '2024-03-18 12:00:00', 'seed', NULL, NULL, 0, NULL, NULL, 'seed-provider-05'),
    (6,  'Augusta Riverwalk Preschool',     (SELECT CountyId FROM County WHERE Name = 'Richmond'),  'Inactive', '2024-04-02 14:20:00', 'seed', '2025-02-10 15:00:00', 'seed', 0, NULL, NULL, 'seed-provider-06'),
    (7,  'Macon Little Scholars',           (SELECT CountyId FROM County WHERE Name = 'Bibb'),      'Pending',  '2025-09-12 17:05:00', 'seed', NULL, NULL, 0, NULL, NULL, 'seed-provider-07'),
    (8,  'Columbus Sunshine Daycare',       (SELECT CountyId FROM County WHERE Name = 'Muscogee'),  'Active',   '2024-05-09 13:40:00', 'seed', NULL, NULL, 0, NULL, NULL, 'seed-provider-08'),
    (9,  'Gwinnett Growing Minds',          (SELECT CountyId FROM County WHERE Name = 'Gwinnett'),  'Active',   '2024-06-21 18:15:00', 'seed', NULL, NULL, 0, NULL, NULL, 'seed-provider-09'),
    (10, 'Valdosta Southside Learning',     (SELECT CountyId FROM County WHERE Name = 'Lowndes'),   'Inactive', '2024-07-08 14:55:00', 'seed', '2025-06-01 13:00:00', 'seed', 0, NULL, NULL, 'seed-provider-10'),
    (11, 'Rome Hilltop Child Care',         (SELECT CountyId FROM County WHERE Name = 'Floyd'),     'Active',   '2024-08-14 15:25:00', 'seed', '2025-04-22 19:30:00', 'seed', 1, '2025-04-22 19:30:00', 'seed', 'seed-provider-11'),
    (12, 'Albany Tiny Tots',                (SELECT CountyId FROM County WHERE Name = 'Dougherty'), 'Inactive', '2024-09-03 12:35:00', 'seed', '2025-01-30 16:10:00', 'seed', 1, '2025-01-30 16:10:00', 'seed', 'seed-provider-12'),
    (13, 'Rome Hilltop Child Care',         (SELECT CountyId FROM County WHERE Name = 'Floyd'),     'Pending',  '2025-08-04 14:00:00', 'seed', NULL, NULL, 0, NULL, NULL, 'seed-provider-13');

INSERT OR IGNORE INTO License
    (LicenseId, ProviderId, LicenseNumber, LicenseStatus, IssueDate, ExpirationDate, CreatedUtc, CreatedBy, ModifiedUtc, ModifiedBy, IsDeleted, DeletedUtc, DeletedBy, ConcurrencyStamp)
VALUES
    (1,  1,  'CCLC-10001', 'Active',    '2024-07-01', '2031-06-30', '2024-07-01 14:00:00', 'seed', NULL, NULL, 0, NULL, NULL, 'seed-license-01'),
    (2,  1,  'CCLC-10002', 'Active',    '2025-01-01', '2030-12-31', '2025-01-01 14:00:00', 'seed', NULL, NULL, 0, NULL, NULL, 'seed-license-02'),
    (3,  2,  'CCLC-10003', 'Expired',   '2022-09-01', '2024-08-31', '2024-02-01 15:30:00', 'seed', NULL, NULL, 0, NULL, NULL, 'seed-license-03'),
    (4,  2,  'CCLC-10004', 'Active',    '2024-09-01', '2030-08-31', '2024-09-01 15:30:00', 'seed', NULL, NULL, 0, NULL, NULL, 'seed-license-04'),
    (5,  3,  'CCLC-10005', 'Active',    '2023-04-01', '2025-03-31', '2024-02-20 13:10:00', 'seed', NULL, NULL, 0, NULL, NULL, 'seed-license-05'),
    (6,  4,  'CCLC-10006', 'Suspended', '2024-06-01', '2030-05-31', '2024-06-01 16:45:00', 'seed', NULL, NULL, 0, NULL, NULL, 'seed-license-06'),
    (7,  6,  'CCLC-10007', 'Expired',   '2022-02-01', '2024-01-31', '2024-04-02 14:20:00', 'seed', NULL, NULL, 0, NULL, NULL, 'seed-license-07'),
    (8,  8,  'CCLC-10008', 'Active',    '2025-02-01', '2032-01-31', '2025-02-01 13:40:00', 'seed', NULL, NULL, 0, NULL, NULL, 'seed-license-08'),
    (9,  8,  'CCLC-10009', 'Active',    '2023-07-01', '2025-06-30', '2024-05-09 13:40:00', 'seed', NULL, NULL, 0, NULL, NULL, 'seed-license-09'),
    (10, 9,  'CCLC-10010', 'Active',    '2024-10-01', '2030-09-30', '2024-10-01 18:15:00', 'seed', NULL, NULL, 0, NULL, NULL, 'seed-license-10'),
    (11, 10, 'CCLC-10011', 'Active',    '2024-04-01', '2030-03-31', '2024-07-08 14:55:00', 'seed', NULL, NULL, 0, NULL, NULL, 'seed-license-11'),
    (12, 11, 'CCLC-10012', 'Active',    '2024-11-01', '2030-10-31', '2024-11-01 15:25:00', 'seed', NULL, NULL, 0, NULL, NULL, 'seed-license-12');
