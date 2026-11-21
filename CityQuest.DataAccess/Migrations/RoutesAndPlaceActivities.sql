START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261104185518_RoutesAndPlaceActivities') THEN
    ALTER TABLE "Places" DROP CONSTRAINT "CK_Places_Category";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261104185518_RoutesAndPlaceActivities') THEN
    ALTER TABLE "Places" ADD "Challenge" character varying(1000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261104185518_RoutesAndPlaceActivities') THEN
    ALTER TABLE "Places" ADD "Fact" character varying(2000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261104185518_RoutesAndPlaceActivities') THEN
    ALTER TABLE "Places" ADD "Hint" character varying(1000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261104185518_RoutesAndPlaceActivities') THEN
    CREATE TABLE "Routes" (
        "Id" uuid NOT NULL,
        "OwnerId" text NOT NULL,
        "Name" character varying(200) NOT NULL,
        "IsPublic" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "StartLatitude" double precision NOT NULL,
        "StartLongitude" double precision NOT NULL,
        "ReturnToStart" boolean NOT NULL,
        "TimeBudgetMinutes" integer NOT NULL,
        "TotalMinutes" integer NOT NULL,
        "WalkingMinutes" integer NOT NULL,
        "DistanceMeters" integer NOT NULL,
        "ReturnWalkingMinutes" integer NOT NULL,
        "ReturnDistanceMeters" integer NOT NULL,
        "Interests" text[] NOT NULL,
        "IncludeFood" boolean NOT NULL,
        "FoodType" character varying(50),
        "CalculationMethod" character varying(50) NOT NULL,
        CONSTRAINT "PK_Routes" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_Routes_TimeBudget" CHECK ("TotalMinutes" > 0 AND "TotalMinutes" <= "TimeBudgetMinutes"),
        CONSTRAINT "FK_Routes_AspNetUsers_OwnerId" FOREIGN KEY ("OwnerId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261104185518_RoutesAndPlaceActivities') THEN
    CREATE TABLE "RouteLikes" (
        "RouteId" uuid NOT NULL,
        "UserId" text NOT NULL,
        CONSTRAINT "PK_RouteLikes" PRIMARY KEY ("RouteId", "UserId"),
        CONSTRAINT "FK_RouteLikes_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_RouteLikes_Routes_RouteId" FOREIGN KEY ("RouteId") REFERENCES "Routes" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261104185518_RoutesAndPlaceActivities') THEN
    CREATE TABLE "RouteStops" (
        "RouteId" uuid NOT NULL,
        "Position" integer NOT NULL,
        "PlaceId" uuid NOT NULL,
        "Name" character varying(200) NOT NULL,
        "Category" character varying(50) NOT NULL,
        "Latitude" double precision NOT NULL,
        "Longitude" double precision NOT NULL,
        "VisitMinutes" integer NOT NULL,
        "WalkingMinutes" integer NOT NULL,
        "DistanceMeters" integer NOT NULL,
        CONSTRAINT "PK_RouteStops" PRIMARY KEY ("RouteId", "Position"),
        CONSTRAINT "FK_RouteStops_Places_PlaceId" FOREIGN KEY ("PlaceId") REFERENCES "Places" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_RouteStops_Routes_RouteId" FOREIGN KEY ("RouteId") REFERENCES "Routes" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261104185518_RoutesAndPlaceActivities') THEN
    CREATE TABLE "SavedRoutes" (
        "RouteId" uuid NOT NULL,
        "UserId" text NOT NULL,
        CONSTRAINT "PK_SavedRoutes" PRIMARY KEY ("RouteId", "UserId"),
        CONSTRAINT "FK_SavedRoutes_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_SavedRoutes_Routes_RouteId" FOREIGN KEY ("RouteId") REFERENCES "Routes" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261104185518_RoutesAndPlaceActivities') THEN
    ALTER TABLE "Places" ADD CONSTRAINT "CK_Places_Category" CHECK ("Category" IN ('architecture', 'nature', 'monument', 'museum', 'cafe', 'restaurant'));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261104185518_RoutesAndPlaceActivities') THEN
    CREATE INDEX "IX_RouteLikes_UserId" ON "RouteLikes" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261104185518_RoutesAndPlaceActivities') THEN
    CREATE INDEX "IX_Routes_IsPublic_CreatedAt" ON "Routes" ("IsPublic", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261104185518_RoutesAndPlaceActivities') THEN
    CREATE INDEX "IX_Routes_OwnerId" ON "Routes" ("OwnerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261104185518_RoutesAndPlaceActivities') THEN
    CREATE INDEX "IX_RouteStops_PlaceId" ON "RouteStops" ("PlaceId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261104185518_RoutesAndPlaceActivities') THEN
    CREATE UNIQUE INDEX "IX_RouteStops_RouteId_PlaceId" ON "RouteStops" ("RouteId", "PlaceId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261104185518_RoutesAndPlaceActivities') THEN
    CREATE INDEX "IX_SavedRoutes_UserId" ON "SavedRoutes" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261104185518_RoutesAndPlaceActivities') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261104185518_RoutesAndPlaceActivities', '10.0.6');
    END IF;
END $EF$;
COMMIT;

