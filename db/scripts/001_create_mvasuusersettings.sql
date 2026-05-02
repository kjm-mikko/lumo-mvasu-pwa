-- =============================================================================
-- 001_create_mvasuusersettings.sql
-- Phase 5 — MVasuUserSettings table for the new mVasu PWA backend.
--
-- This table is referenced by src/mVasu.Api/Domain/MVasuUserSettings.cs as a
-- BaseObject derivative. XPO is configured with AutoCreateOption.SchemaAlreadyExists
-- so the runtime will never create or alter this table — apply this script
-- manually before the first /api/me call lands on a fresh environment.
--
-- Target: SQL Server. Adjust collation / schema name as required.
-- =============================================================================

CREATE TABLE [dbo].[MVasuUserSettings]
(
    [Oid]                 uniqueidentifier NOT NULL,
    [User]                uniqueidentifier NULL,
    [PreferredName]       nvarchar(100)    NULL,
    [Theme]               nvarchar(20)     NULL,
    [Language]            nvarchar(10)     NULL,
    [LocationConsent]     bit              NOT NULL CONSTRAINT [DF_MVasuUserSettings_LocationConsent] DEFAULT (0),
    [SettingsJson]        nvarchar(max)    NULL,
    [OptimisticLockField] int              NULL,
    [GCRecord]            int              NULL,
    CONSTRAINT [PK_MVasuUserSettings] PRIMARY KEY CLUSTERED ([Oid] ASC)
);
GO

-- XPO filters out soft-deleted rows by GCRecord IS NULL, so an index on
-- GCRecord lets that filter use a seek instead of a scan.
CREATE NONCLUSTERED INDEX [iGCRecord_MVasuUserSettings]
    ON [dbo].[MVasuUserSettings] ([GCRecord]);
GO

-- Lookup index for the 1:1 relation back to xVasuSecuritySystemUser.Oid.
CREATE NONCLUSTERED INDEX [iUser_MVasuUserSettings]
    ON [dbo].[MVasuUserSettings] ([User]);
GO

-- -----------------------------------------------------------------------------
-- Optional foreign key to xVasuSecuritySystemUser(Oid).
-- xVasuSecuritySystemUser inherits from PermissionPolicyUser; depending on the
-- mapping strategy in your database the Oid may live on the base table.
-- Verify the actual referenced table in your database before applying.
--
-- ALTER TABLE [dbo].[MVasuUserSettings]
--     ADD CONSTRAINT [FK_MVasuUserSettings_User]
--     FOREIGN KEY ([User]) REFERENCES [dbo].[xVasuSecuritySystemUser] ([Oid]);
-- GO
