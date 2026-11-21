START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261104190806_RoadRouteGeometry') THEN
    ALTER TABLE "Routes" ADD "Attribution" character varying(1000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261104190806_RoadRouteGeometry') THEN
    ALTER TABLE "Routes" ADD "GeometryJson" jsonb;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261104190806_RoadRouteGeometry') THEN
    ALTER TABLE "Routes" ADD "RoadWarnings" text[] NOT NULL DEFAULT ARRAY[]::text[];
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261104190806_RoadRouteGeometry') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261104190806_RoadRouteGeometry', '10.0.6');
    END IF;
END $EF$;
COMMIT;

