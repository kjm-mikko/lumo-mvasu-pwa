-- =============================================================================
-- 002_add_location_to_mvasuusersettings.sql
-- Phase 7 — extends MVasuUserSettings with last-known-location columns.
--
-- Apply this script manually after 001 has been applied. The runtime never
-- emits DDL (AutoCreateOption.SchemaAlreadyExists), so the columns must be
-- created here before /api/me/location starts persisting data.
--
-- Target: SQL Server.
-- =============================================================================

ALTER TABLE [dbo].[MVasuUserSettings]
    ADD [LastLocationLatitude]        float          NULL,
        [LastLocationLongitude]       float          NULL,
        [LastLocationAccuracyMeters]  float          NULL,
        [LastLocationRecordedAt]      datetimeoffset NULL;
GO
