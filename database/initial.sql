CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) NOT NULL,
    `ProductVersion` varchar(32) NOT NULL,
    PRIMARY KEY (`MigrationId`)
);

START TRANSACTION;
CREATE TABLE `category` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Name` varchar(255) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    PRIMARY KEY (`Id`)
);

CREATE TABLE `qr_code` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Guid` char(36) NOT NULL,
    `Code` varchar(50) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    PRIMARY KEY (`Id`)
);

CREATE TABLE `user` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `TenantId` char(36) NOT NULL,
    `ObjectId` char(36) NOT NULL,
    `Name` varchar(255) NOT NULL,
    `Email` varchar(255) NOT NULL,
    `IsAdmin` tinyint(1) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    PRIMARY KEY (`Id`)
);

CREATE TABLE `item` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `CategoryId` int NOT NULL,
    `QRCodeId` int NULL,
    `Name` varchar(255) NOT NULL,
    `Description` varchar(2000) NOT NULL,
    `Location` varchar(255) NOT NULL,
    `Status` varchar(20) NOT NULL,
    `FoundOn` datetime(6) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    `StatusChangedAt` datetime(6) NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_item_category_CategoryId` FOREIGN KEY (`CategoryId`) REFERENCES `category` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_item_qr_code_QRCodeId` FOREIGN KEY (`QRCodeId`) REFERENCES `qr_code` (`Id`) ON DELETE RESTRICT
);

CREATE TABLE `activity_log` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `ItemId` int NOT NULL,
    `UserId` int NULL,
    `Type` varchar(40) NOT NULL,
    `Description` varchar(3000) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_activity_log_item_ItemId` FOREIGN KEY (`ItemId`) REFERENCES `item` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_activity_log_user_UserId` FOREIGN KEY (`UserId`) REFERENCES `user` (`Id`) ON DELETE RESTRICT
);

CREATE TABLE `recovery_request` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `ItemId` int NOT NULL,
    `UserId` int NOT NULL,
    `Justification` varchar(2000) NOT NULL,
    `AdminNotes` varchar(2000) NULL,
    `Status` varchar(20) NOT NULL,
    `ExpiresAt` datetime(6) NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_recovery_request_item_ItemId` FOREIGN KEY (`ItemId`) REFERENCES `item` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_recovery_request_user_UserId` FOREIGN KEY (`UserId`) REFERENCES `user` (`Id`) ON DELETE RESTRICT
);

CREATE INDEX `IX_activity_log_ItemId_CreatedAt` ON `activity_log` (`ItemId`, `CreatedAt`);

CREATE INDEX `IX_activity_log_UserId` ON `activity_log` (`UserId`);

CREATE UNIQUE INDEX `IX_category_Name` ON `category` (`Name`);

CREATE INDEX `IX_item_CategoryId` ON `item` (`CategoryId`);

CREATE UNIQUE INDEX `IX_item_QRCodeId` ON `item` (`QRCodeId`);

CREATE INDEX `IX_item_Status_StatusChangedAt` ON `item` (`Status`, `StatusChangedAt`);

CREATE UNIQUE INDEX `IX_qr_code_Code` ON `qr_code` (`Code`);

CREATE UNIQUE INDEX `IX_qr_code_Guid` ON `qr_code` (`Guid`);

CREATE INDEX `IX_recovery_request_ItemId_Status` ON `recovery_request` (`ItemId`, `Status`);

CREATE INDEX `IX_recovery_request_Status_ExpiresAt` ON `recovery_request` (`Status`, `ExpiresAt`);

CREATE INDEX `IX_recovery_request_UserId` ON `recovery_request` (`UserId`);

CREATE UNIQUE INDEX `IX_user_TenantId_ObjectId` ON `user` (`TenantId`, `ObjectId`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260927102319_InitialCreate', '10.0.12');

DROP INDEX IX_qr_code_Code ON qr_code;

ALTER TABLE `qr_code` DROP COLUMN `Code`;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261015051243_RemoveRedundantQRCodeCode', '10.0.12');

CREATE TABLE `item_photo` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `ItemId` int NOT NULL,
    `FileName` varchar(100) NOT NULL,
    `ContentType` varchar(30) NOT NULL,
    `Size` bigint NOT NULL,
    `SortOrder` int NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_item_photo_item_ItemId` FOREIGN KEY (`ItemId`) REFERENCES `item` (`Id`) ON DELETE CASCADE
);

CREATE INDEX `IX_item_photo_ItemId_SortOrder` ON `item_photo` (`ItemId`, `SortOrder`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261016050615_AddItemPhotos', '10.0.12');

ALTER TABLE `recovery_request` ADD `PickupCodeEncrypted` varchar(1000) NULL;

ALTER TABLE `recovery_request` ADD `PickupCodeFailedAttempts` int NOT NULL DEFAULT 0;

ALTER TABLE `recovery_request` ADD `PickupCodeLockedUntil` datetime(6) NULL;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261016070155_AddPickupCode', '10.0.12');

COMMIT;

