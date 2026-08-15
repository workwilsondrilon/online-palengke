-- =============================================================================
-- 002_catalog_and_markets.sql
--
-- Catalog and markets for Epic 3: the admin-curated item vocabulary, the wet
-- markets themselves, their delivery windows, and the two FKs on `partners`
-- that script 001 deliberately left dangling.
--
--   categories, units, items, item_units  -- catalog
--   markets, delivery_windows             -- markets & geofencing
--
-- Conventions: see the header of 001_identity_and_media.sql. All of those
-- still apply here (snake_case, BIGINT UNSIGNED surrogate keys, DATETIME(6)
-- UTC timestamps written explicitly, VARCHAR + named CHECK instead of ENUM).
--
-- One addition this script needs that 001 didn't: geometry.
--
--   * `markets.service_area` is `POLYGON NULL SRID 4326`. The plan's data
--     model sketch has it NOT NULL with a SPATIAL INDEX, but that is not
--     achievable together with the real admin workflow: a market is created
--     first and its polygon is drawn afterwards on the market's own detail
--     page (there is nowhere to draw an area before the market exists), so
--     the column has to tolerate "not drawn yet". MySQL enforces "All parts
--     of a SPATIAL index must be NOT NULL" (verified against the local
--     server), so a nullable geometry column cannot carry one. Given the
--     realistic scale of a wet-market network (dozens of markets, not
--     thousands), `ST_Contains` doing a full scan over
--     `WHERE service_area IS NOT NULL` is not a performance concern.
--     Revisit only if the market count grows enough to matter.
--   * Axis order: WKT under SRID 4326 is **latitude, longitude** in MySQL 8
--     (the EPSG:4326 standard axis order) -- the reverse of GeoJSON, which is
--     always longitude, latitude. Verified directly:
--     `ST_GeomFromText('POINT(14.667 121.0)', 4326)` is Manila; swapping the
--     two throws "Latitude ... is out of range". Leaflet.draw (task #30)
--     produces and consumes GeoJSON, so whatever Infrastructure code
--     round-trips `Market.ServiceAreaWkt` (task #27) must flip coordinate
--     order at that boundary. Getting this backwards would not fail loudly --
--     it would silently geofence the wrong hemisphere-ish location, so this
--     needs its own explicit round-trip test in task #27, not just review.
-- =============================================================================


-- -----------------------------------------------------------------------------
-- categories
--
-- Admin-curated catalog buckets ("Meat", "Fish & seafood"). A shopping-list
-- request splits into one mini-RFQ per category (Epic 5): one winning stall
-- per category, not per item.
-- -----------------------------------------------------------------------------
CREATE TABLE categories (
    id         BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    name       VARCHAR(100)    NOT NULL,
    slug       VARCHAR(100)    NOT NULL,
    sort_order INT             NOT NULL DEFAULT 0,
    is_active  BOOLEAN         NOT NULL DEFAULT TRUE,
    created_at DATETIME(6)     NOT NULL,
    updated_at DATETIME(6)     NOT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uq_categories_name (name),
    UNIQUE KEY uq_categories_slug (slug)
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_0900_ai_ci;


-- -----------------------------------------------------------------------------
-- units
--
-- Units of measure a shopping-list line or a quote line can be priced in --
-- kg, piece, bundle, tali. `code` is the stable machine identifier referenced
-- by items and quote lines; `name` is what an operator sees on a form.
-- -----------------------------------------------------------------------------
CREATE TABLE units (
    id                         BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    code                       VARCHAR(20)     NOT NULL,
    name                       VARCHAR(50)     NOT NULL,
    allows_fractional_quantity BOOLEAN         NOT NULL DEFAULT TRUE,
    is_active                  BOOLEAN         NOT NULL DEFAULT TRUE,
    created_at                 DATETIME(6)     NOT NULL,
    updated_at                 DATETIME(6)     NOT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uq_units_code (code)
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_0900_ai_ci;


-- -----------------------------------------------------------------------------
-- items
--
-- The item master a shopping list is built from. Deliberately carries no
-- price anywhere -- prices only ever exist on a partner's quote (Epic 5/6).
-- media_asset_id is ON DELETE SET NULL rather than RESTRICT: an item photo is
-- decorative, not evidence (unlike a KYC document), so if its media_assets
-- row is ever removed the item should degrade to "no photo", not block the
-- delete.
-- -----------------------------------------------------------------------------
CREATE TABLE items (
    id                 BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    category_id        BIGINT UNSIGNED NOT NULL,
    name               VARCHAR(150)    NOT NULL,
    media_asset_id     BIGINT UNSIGNED NULL,
    weight_per_unit_kg DECIMAL(8, 3)   NOT NULL,
    is_active          BOOLEAN         NOT NULL DEFAULT TRUE,
    created_at         DATETIME(6)     NOT NULL,
    updated_at         DATETIME(6)     NOT NULL,
    PRIMARY KEY (id),
    KEY idx_items_category (category_id),
    KEY idx_items_media_asset (media_asset_id),
    CONSTRAINT fk_items_category
        FOREIGN KEY (category_id) REFERENCES categories (id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_items_media_asset
        FOREIGN KEY (media_asset_id) REFERENCES media_assets (id)
        ON DELETE SET NULL,
    CONSTRAINT chk_items_weight_per_unit_kg CHECK (weight_per_unit_kg > 0)
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_0900_ai_ci;


-- -----------------------------------------------------------------------------
-- item_units
--
-- Which units an item may be ordered in -- pork can be "by kg" or "by piece",
-- each a separate row. is_default marks the unit shown by default when
-- building a shopping-list line; "at most one default per item" is enforced
-- by the repository, not the database (MySQL has no native partial-unique
-- constraint for that). unit_id is ON DELETE RESTRICT: units are a shared
-- reference table, not owned by any one item.
-- -----------------------------------------------------------------------------
CREATE TABLE item_units (
    id         BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    item_id    BIGINT UNSIGNED NOT NULL,
    unit_id    BIGINT UNSIGNED NOT NULL,
    is_default BOOLEAN         NOT NULL DEFAULT FALSE,
    PRIMARY KEY (id),
    UNIQUE KEY uq_item_units_item_unit (item_id, unit_id),
    KEY idx_item_units_unit (unit_id),
    CONSTRAINT fk_item_units_item
        FOREIGN KEY (item_id) REFERENCES items (id)
        ON DELETE CASCADE,
    CONSTRAINT fk_item_units_unit
        FOREIGN KEY (unit_id) REFERENCES units (id)
        ON DELETE RESTRICT
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_0900_ai_ci;


-- -----------------------------------------------------------------------------
-- markets
--
-- A wet market in the network. lat/lng are the market's own pin (the origin
-- point of the rider fee's distance calculation, decision 12) -- DECIMAL(10,7)
-- for the same exactness reason as customer_addresses.lat/lng, since money is
-- derived from them. service_area is the admin-drawn delivery polygon; see
-- the nullability and axis-order notes in this file's header before touching
-- it from Infrastructure.
-- -----------------------------------------------------------------------------
CREATE TABLE markets (
    id            BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    name          VARCHAR(150)    NOT NULL,
    address       VARCHAR(255)    NOT NULL,
    city          VARCHAR(100)    NOT NULL,
    province      VARCHAR(100)    NOT NULL,
    lat           DECIMAL(10, 7)  NOT NULL,
    lng           DECIMAL(10, 7)  NOT NULL,
    service_area  POLYGON         NULL SRID 4326,
    status        VARCHAR(20)     NOT NULL,
    created_at    DATETIME(6)     NOT NULL,
    updated_at    DATETIME(6)     NOT NULL,
    PRIMARY KEY (id),
    KEY idx_markets_status (status),
    KEY idx_markets_city_province (city, province),
    CONSTRAINT chk_markets_status
        CHECK (status IN ('onboarding', 'active', 'suspended')),
    CONSTRAINT chk_markets_lat CHECK (lat BETWEEN -90 AND 90),
    CONSTRAINT chk_markets_lng CHECK (lng BETWEEN -180 AND 180)
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_0900_ai_ci;


-- -----------------------------------------------------------------------------
-- delivery_windows
--
-- A recurring delivery slot for one market -- "7-9 AM", every day. starts_at
-- and ends_at are plain TIME holding Asia/Manila wall-clock, not UTC: this is
-- a recurring schedule, not an instant, and converting "7 AM" to UTC here
-- would store 23:00 the previous day, which no operator editing this table
-- would recognise. The conversion to a real instant happens per delivery
-- date, in the Application layer.
-- -----------------------------------------------------------------------------
CREATE TABLE delivery_windows (
    id                    BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    market_id             BIGINT UNSIGNED NOT NULL,
    label                 VARCHAR(50)     NOT NULL,
    starts_at             TIME            NOT NULL,
    ends_at               TIME            NOT NULL,
    cutoff_offset_minutes INT             NOT NULL,
    capacity              INT             NOT NULL,
    is_active             BOOLEAN         NOT NULL DEFAULT TRUE,
    created_at            DATETIME(6)     NOT NULL,
    updated_at            DATETIME(6)     NOT NULL,
    PRIMARY KEY (id),
    KEY idx_delivery_windows_market (market_id),
    CONSTRAINT fk_delivery_windows_market
        FOREIGN KEY (market_id) REFERENCES markets (id)
        ON DELETE CASCADE,
    CONSTRAINT chk_delivery_windows_ends_after_starts CHECK (ends_at > starts_at),
    CONSTRAINT chk_delivery_windows_cutoff_offset_minutes CHECK (cutoff_offset_minutes >= 0),
    CONSTRAINT chk_delivery_windows_capacity CHECK (capacity > 0)
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_0900_ai_ci;


-- -----------------------------------------------------------------------------
-- Backfill: partners.market_id / partners.category_id
--
-- See the NOTE: comment above `CREATE TABLE partners` in 001 -- those columns
-- were left as bare nullable BIGINT UNSIGNED because markets and categories
-- did not exist yet. Both tables now exist, so the deferred FKs land here.
-- RESTRICT on both: a partner's market/category assignment must be explicitly
-- reassigned before the referenced row can be removed, matching how every
-- other cross-reference in this schema treats a shared lookup table.
-- -----------------------------------------------------------------------------
ALTER TABLE partners
    ADD CONSTRAINT fk_partners_market
        FOREIGN KEY (market_id) REFERENCES markets (id)
        ON DELETE RESTRICT,
    ADD CONSTRAINT fk_partners_category
        FOREIGN KEY (category_id) REFERENCES categories (id)
        ON DELETE RESTRICT;
