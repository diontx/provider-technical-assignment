CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
    "ProductVersion" TEXT NOT NULL
);

BEGIN TRANSACTION;

CREATE TABLE "County" (
    "CountyId" INTEGER NOT NULL CONSTRAINT "PK_County" PRIMARY KEY,
    "Name" TEXT COLLATE NOCASE NOT NULL
);

CREATE TABLE "Provider" (
    "ProviderId" INTEGER NOT NULL CONSTRAINT "PK_Provider" PRIMARY KEY AUTOINCREMENT,
    "ProviderName" TEXT COLLATE NOCASE NOT NULL,
    "CountyId" INTEGER NOT NULL,
    "Status" TEXT NOT NULL,
    "CreatedUtc" TEXT NOT NULL,
    "CreatedBy" TEXT NOT NULL,
    "ModifiedUtc" TEXT NULL,
    "ModifiedBy" TEXT NULL,
    "IsDeleted" INTEGER NOT NULL DEFAULT 0,
    "DeletedUtc" TEXT NULL,
    "DeletedBy" TEXT NULL,
    "ConcurrencyStamp" TEXT NOT NULL,
    CONSTRAINT "CK_Provider_IsDeleted" CHECK (IsDeleted IN (0, 1)),
    CONSTRAINT "CK_Provider_ProviderName" CHECK (length(trim(ProviderName)) BETWEEN 1 AND 200),
    CONSTRAINT "CK_Provider_SoftDelete" CHECK ((IsDeleted = 0 AND DeletedUtc IS NULL) OR (IsDeleted = 1 AND DeletedUtc IS NOT NULL)),
    CONSTRAINT "CK_Provider_Status" CHECK (Status IN ('Active', 'Inactive', 'Pending')),
    CONSTRAINT "FK_Provider_County_CountyId" FOREIGN KEY ("CountyId") REFERENCES "County" ("CountyId") ON DELETE RESTRICT
);

CREATE TABLE "License" (
    "LicenseId" INTEGER NOT NULL CONSTRAINT "PK_License" PRIMARY KEY AUTOINCREMENT,
    "ProviderId" INTEGER NOT NULL,
    "LicenseNumber" TEXT COLLATE NOCASE NOT NULL,
    "LicenseStatus" TEXT NOT NULL,
    "IssueDate" TEXT NULL,
    "ExpirationDate" TEXT NOT NULL,
    "CreatedUtc" TEXT NOT NULL,
    "CreatedBy" TEXT NOT NULL,
    "ModifiedUtc" TEXT NULL,
    "ModifiedBy" TEXT NULL,
    "IsDeleted" INTEGER NOT NULL DEFAULT 0,
    "DeletedUtc" TEXT NULL,
    "DeletedBy" TEXT NULL,
    "ConcurrencyStamp" TEXT NOT NULL,
    CONSTRAINT "CK_License_IsDeleted" CHECK (IsDeleted IN (0, 1)),
    CONSTRAINT "CK_License_LicenseNumber" CHECK (length(trim(LicenseNumber)) BETWEEN 1 AND 50),
    CONSTRAINT "CK_License_LicenseStatus" CHECK (LicenseStatus IN ('Active', 'Expired', 'Suspended')),
    CONSTRAINT "CK_License_SoftDelete" CHECK ((IsDeleted = 0 AND DeletedUtc IS NULL) OR (IsDeleted = 1 AND DeletedUtc IS NOT NULL)),
    CONSTRAINT "FK_License_Provider_ProviderId" FOREIGN KEY ("ProviderId") REFERENCES "Provider" ("ProviderId") ON DELETE RESTRICT
);

INSERT INTO "County" ("CountyId", "Name")
VALUES (1, 'Appling');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (2, 'Atkinson');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (3, 'Bacon');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (4, 'Baker');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (5, 'Baldwin');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (6, 'Banks');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (7, 'Barrow');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (8, 'Bartow');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (9, 'Ben Hill');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (10, 'Berrien');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (11, 'Bibb');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (12, 'Bleckley');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (13, 'Brantley');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (14, 'Brooks');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (15, 'Bryan');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (16, 'Bulloch');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (17, 'Burke');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (18, 'Butts');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (19, 'Calhoun');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (20, 'Camden');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (21, 'Candler');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (22, 'Carroll');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (23, 'Catoosa');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (24, 'Charlton');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (25, 'Chatham');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (26, 'Chattahoochee');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (27, 'Chattooga');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (28, 'Cherokee');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (29, 'Clarke');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (30, 'Clay');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (31, 'Clayton');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (32, 'Clinch');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (33, 'Cobb');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (34, 'Coffee');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (35, 'Colquitt');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (36, 'Columbia');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (37, 'Cook');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (38, 'Coweta');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (39, 'Crawford');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (40, 'Crisp');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (41, 'Dade');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (42, 'Dawson');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (43, 'Decatur');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (44, 'DeKalb');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (45, 'Dodge');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (46, 'Dooly');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (47, 'Dougherty');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (48, 'Douglas');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (49, 'Early');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (50, 'Echols');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (51, 'Effingham');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (52, 'Elbert');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (53, 'Emanuel');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (54, 'Evans');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (55, 'Fannin');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (56, 'Fayette');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (57, 'Floyd');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (58, 'Forsyth');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (59, 'Franklin');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (60, 'Fulton');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (61, 'Gilmer');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (62, 'Glascock');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (63, 'Glynn');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (64, 'Gordon');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (65, 'Grady');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (66, 'Greene');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (67, 'Gwinnett');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (68, 'Habersham');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (69, 'Hall');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (70, 'Hancock');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (71, 'Haralson');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (72, 'Harris');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (73, 'Hart');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (74, 'Heard');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (75, 'Henry');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (76, 'Houston');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (77, 'Irwin');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (78, 'Jackson');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (79, 'Jasper');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (80, 'Jeff Davis');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (81, 'Jefferson');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (82, 'Jenkins');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (83, 'Johnson');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (84, 'Jones');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (85, 'Lamar');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (86, 'Lanier');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (87, 'Laurens');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (88, 'Lee');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (89, 'Liberty');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (90, 'Lincoln');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (91, 'Long');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (92, 'Lowndes');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (93, 'Lumpkin');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (94, 'Macon');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (95, 'Madison');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (96, 'Marion');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (97, 'McDuffie');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (98, 'McIntosh');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (99, 'Meriwether');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (100, 'Miller');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (101, 'Mitchell');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (102, 'Monroe');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (103, 'Montgomery');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (104, 'Morgan');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (105, 'Murray');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (106, 'Muscogee');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (107, 'Newton');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (108, 'Oconee');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (109, 'Oglethorpe');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (110, 'Paulding');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (111, 'Peach');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (112, 'Pickens');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (113, 'Pierce');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (114, 'Pike');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (115, 'Polk');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (116, 'Pulaski');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (117, 'Putnam');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (118, 'Quitman');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (119, 'Rabun');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (120, 'Randolph');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (121, 'Richmond');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (122, 'Rockdale');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (123, 'Schley');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (124, 'Screven');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (125, 'Seminole');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (126, 'Spalding');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (127, 'Stephens');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (128, 'Stewart');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (129, 'Sumter');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (130, 'Talbot');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (131, 'Taliaferro');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (132, 'Tattnall');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (133, 'Taylor');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (134, 'Telfair');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (135, 'Terrell');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (136, 'Thomas');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (137, 'Tift');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (138, 'Toombs');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (139, 'Towns');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (140, 'Treutlen');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (141, 'Troup');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (142, 'Turner');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (143, 'Twiggs');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (144, 'Union');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (145, 'Upson');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (146, 'Walker');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (147, 'Walton');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (148, 'Ware');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (149, 'Warren');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (150, 'Washington');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (151, 'Wayne');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (152, 'Webster');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (153, 'Wheeler');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (154, 'White');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (155, 'Whitfield');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (156, 'Wilcox');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (157, 'Wilkes');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (158, 'Wilkinson');
SELECT changes();

INSERT INTO "County" ("CountyId", "Name")
VALUES (159, 'Worth');
SELECT changes();


CREATE UNIQUE INDEX "UX_County_Name" ON "County" ("Name");

CREATE INDEX "IX_License_Provider_Expiration" ON "License" ("ProviderId", "ExpirationDate") WHERE IsDeleted = 0;

CREATE UNIQUE INDEX "UX_License_Provider_Number" ON "License" ("ProviderId", "LicenseNumber");

CREATE INDEX "IX_Provider_CountyId" ON "Provider" ("CountyId");

CREATE INDEX "IX_Provider_Name" ON "Provider" ("ProviderName") WHERE IsDeleted = 0;

CREATE INDEX "IX_Provider_Status_County" ON "Provider" ("Status", "CountyId") WHERE IsDeleted = 0;

CREATE UNIQUE INDEX "UX_Provider_Name_County" ON "Provider" ("ProviderName", "CountyId") WHERE IsDeleted = 0;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261010164122_InitialSchema', '8.0.31');

COMMIT;

BEGIN TRANSACTION;

CREATE TRIGGER TR_Provider_BlockDelete
BEFORE DELETE ON Provider
BEGIN
    SELECT RAISE(ABORT, 'Hard delete is not allowed on Provider. Set IsDeleted = 1 instead.');
END;

CREATE TRIGGER TR_License_BlockDelete
BEFORE DELETE ON License
BEGIN
    SELECT RAISE(ABORT, 'Hard delete is not allowed on License. Set IsDeleted = 1 instead.');
END;

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

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261010164133_SoftDeleteTriggersAndViews', '8.0.31');

COMMIT;

