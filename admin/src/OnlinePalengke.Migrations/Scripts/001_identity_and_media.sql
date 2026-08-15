-- =============================================================================
-- 001_identity_and_media.sql
--
-- Foundation schema for Online Palengke: identity and the profiles hanging off
-- it, plus the shared media-asset registry.
--
--   users, otp_codes, refresh_tokens, device_tokens  -- authentication
--   media_assets                                     -- S3 object registry
--   customers, customer_addresses                    -- customer profiles
--   partners, riders                                 -- supply-side profiles
--
-- Conventions enforced here and in every later script:
--   * InnoDB, utf8mb4 / utf8mb4_0900_ai_ci.
--   * snake_case columns. A Dapper type map converts to PascalCase in C#.
--   * Surrogate keys are BIGINT UNSIGNED AUTO_INCREMENT.
--   * All timestamps are DATETIME(6) holding UTC. Never TIMESTAMP (it converts
--     on the way in and out) and never ON UPDATE CURRENT_TIMESTAMP -- the
--     application writes created_at/updated_at explicitly so that the value is
--     the same one the application already has in hand.
--   * Money is DECIMAL(12,2). No money columns appear in this script.
--   * Enum-ish columns are VARCHAR + a named CHECK constraint rather than
--     MySQL ENUM, so adding a value is a CHECK swap and not a table rebuild.
-- =============================================================================


-- -----------------------------------------------------------------------------
-- users
--
-- One row per human. Mobile users (customer/partner/rider) authenticate by
-- phone plus OTP and therefore have no password_hash. Admins sign in to the
-- back office with email plus password and have no phone. Both columns are
-- nullable to allow that, so a CHECK enforces that at least one is present.
-- -----------------------------------------------------------------------------
CREATE TABLE users (
    id            BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    phone         VARCHAR(20)     NULL,
    email         VARCHAR(255)    NULL,
    password_hash VARCHAR(255)    NULL,
    role          VARCHAR(20)     NOT NULL,
    status        VARCHAR(20)     NOT NULL,
    created_at    DATETIME(6)     NOT NULL,
    updated_at    DATETIME(6)     NOT NULL,
    PRIMARY KEY (id),
    -- MySQL permits many NULLs in a UNIQUE index, which is exactly what we want
    -- here: every admin has a NULL phone, every mobile user a NULL email.
    UNIQUE KEY uq_users_phone (phone),
    UNIQUE KEY uq_users_email (email),
    KEY idx_users_role_status (role, status),
    CONSTRAINT chk_users_role
        CHECK (role IN ('customer', 'partner', 'rider', 'admin')),
    CONSTRAINT chk_users_status
        CHECK (status IN ('active', 'suspended', 'deleted')),
    CONSTRAINT chk_users_contact_present
        CHECK (phone IS NOT NULL OR email IS NOT NULL)
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_0900_ai_ci;


-- -----------------------------------------------------------------------------
-- otp_codes
--
-- Short-lived phone verification codes. Only a hash of the code is stored, so a
-- database leak does not hand an attacker a working login. Rows are keyed on
-- phone rather than user_id because the code is issued before we know (or
-- create) the user. attempt_count backs the "too many wrong guesses" lockout.
-- A periodic job deletes rows whose expires_at is well in the past.
-- -----------------------------------------------------------------------------
CREATE TABLE otp_codes (
    id            BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    phone         VARCHAR(20)     NOT NULL,
    code_hash     VARCHAR(255)    NOT NULL,
    expires_at    DATETIME(6)     NOT NULL,
    consumed_at   DATETIME(6)     NULL,
    attempt_count INT             NOT NULL DEFAULT 0,
    created_at    DATETIME(6)     NOT NULL,
    PRIMARY KEY (id),
    -- Verification looks up the newest unexpired code for a phone number.
    KEY idx_otp_codes_phone_expires (phone, expires_at),
    CONSTRAINT chk_otp_codes_attempt_count CHECK (attempt_count >= 0)
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_0900_ai_ci;


-- -----------------------------------------------------------------------------
-- refresh_tokens
--
-- Rotating refresh tokens. Only the hash is stored. revoked_at is set on logout
-- and on rotation so a replayed token can be detected rather than merely
-- rejected. Deleting a user takes their sessions with them.
-- -----------------------------------------------------------------------------
CREATE TABLE refresh_tokens (
    id         BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    user_id    BIGINT UNSIGNED NOT NULL,
    token_hash VARCHAR(255)    NOT NULL,
    expires_at DATETIME(6)     NOT NULL,
    revoked_at DATETIME(6)     NULL,
    created_at DATETIME(6)     NOT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uq_refresh_tokens_token_hash (token_hash),
    KEY idx_refresh_tokens_user (user_id),
    CONSTRAINT fk_refresh_tokens_user
        FOREIGN KEY (user_id) REFERENCES users (id)
        ON DELETE CASCADE
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_0900_ai_ci;


-- -----------------------------------------------------------------------------
-- device_tokens
--
-- Firebase Cloud Messaging registration tokens for push notifications. The FCM
-- token is unique across the whole table, not per user: when a device is handed
-- over or a user signs in on someone else's handset, FCM reissues the same
-- token to the new user and the old row must be reassigned, not duplicated.
-- -----------------------------------------------------------------------------
CREATE TABLE device_tokens (
    id           BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    user_id      BIGINT UNSIGNED NOT NULL,
    fcm_token    VARCHAR(512)    NOT NULL,
    platform     VARCHAR(10)     NOT NULL,
    last_seen_at DATETIME(6)     NOT NULL,
    created_at   DATETIME(6)     NOT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uq_device_tokens_fcm_token (fcm_token),
    KEY idx_device_tokens_user (user_id),
    CONSTRAINT fk_device_tokens_user
        FOREIGN KEY (user_id) REFERENCES users (id)
        ON DELETE CASCADE,
    CONSTRAINT chk_device_tokens_platform
        CHECK (platform IN ('ios', 'android'))
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_0900_ai_ci;


-- -----------------------------------------------------------------------------
-- media_assets
--
-- Registry of every object we hold in S3 (real S3 in every environment -- there
-- is no local storage emulator). Rows are created in 'pending' state when a
-- presigned upload URL is handed out and flipped to 'committed' once the client
-- confirms the upload and the owning entity references the asset. A daily job
-- sweeps 'pending' rows older than 24 hours and deletes the orphaned objects,
-- which is what idx_media_assets_state_created serves.
--
-- uploaded_by is ON DELETE RESTRICT rather than CASCADE: KYC documents and
-- delivery proofs are evidence and must outlive a user record, so deleting a
-- user who still owns assets has to be an explicit, deliberate cleanup.
-- -----------------------------------------------------------------------------
CREATE TABLE media_assets (
    id           BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    bucket       VARCHAR(100)    NOT NULL,
    object_key   VARCHAR(512)    NOT NULL,
    content_type VARCHAR(100)    NOT NULL,
    size_bytes   BIGINT          NOT NULL,
    -- 'kyc_document', 'partner_product', 'item_image', 'delivery_proof',
    -- 'profile_photo'. Deliberately left without a CHECK: new purposes appear
    -- with every feature and are validated in the application layer.
    purpose      VARCHAR(50)     NOT NULL,
    uploaded_by  BIGINT UNSIGNED NOT NULL,
    state        VARCHAR(20)     NOT NULL,
    created_at   DATETIME(6)     NOT NULL,
    committed_at DATETIME(6)     NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uq_media_assets_bucket_object_key (bucket, object_key),
    KEY idx_media_assets_state_created (state, created_at),
    KEY idx_media_assets_uploaded_by (uploaded_by),
    KEY idx_media_assets_purpose (purpose),
    CONSTRAINT fk_media_assets_uploaded_by
        FOREIGN KEY (uploaded_by) REFERENCES users (id)
        ON DELETE RESTRICT,
    CONSTRAINT chk_media_assets_state
        CHECK (state IN ('pending', 'committed')),
    CONSTRAINT chk_media_assets_size_bytes
        CHECK (size_bytes >= 0)
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_0900_ai_ci;


-- -----------------------------------------------------------------------------
-- customers
--
-- Customer profile, one-to-one with users. Kept separate from users so that
-- role-specific fields do not accumulate on the identity table.
-- -----------------------------------------------------------------------------
CREATE TABLE customers (
    id         BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    user_id    BIGINT UNSIGNED NOT NULL,
    full_name  VARCHAR(150)    NOT NULL,
    created_at DATETIME(6)     NOT NULL,
    updated_at DATETIME(6)     NOT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uq_customers_user (user_id),
    CONSTRAINT fk_customers_user
        FOREIGN KEY (user_id) REFERENCES users (id)
        ON DELETE CASCADE
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_0900_ai_ci;


-- -----------------------------------------------------------------------------
-- customer_addresses
--
-- Delivery addresses. lat/lng are DECIMAL(10,7) -- roughly 1 cm of resolution
-- and, unlike DOUBLE, exact, which matters because rider distance and delivery
-- fee bands are computed from these values. is_default is advisory and the
-- application is responsible for keeping at most one default per customer.
-- -----------------------------------------------------------------------------
CREATE TABLE customer_addresses (
    id          BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    customer_id BIGINT UNSIGNED NOT NULL,
    label       VARCHAR(50)     NULL,
    line1       VARCHAR(255)    NOT NULL,
    barangay    VARCHAR(100)    NULL,
    city        VARCHAR(100)    NOT NULL,
    lat         DECIMAL(10, 7)  NOT NULL,
    lng         DECIMAL(10, 7)  NOT NULL,
    is_default  BOOLEAN         NOT NULL DEFAULT FALSE,
    created_at  DATETIME(6)     NOT NULL,
    updated_at  DATETIME(6)     NOT NULL,
    PRIMARY KEY (id),
    KEY idx_customer_addresses_customer (customer_id),
    CONSTRAINT fk_customer_addresses_customer
        FOREIGN KEY (customer_id) REFERENCES customers (id)
        ON DELETE CASCADE,
    CONSTRAINT chk_customer_addresses_lat CHECK (lat BETWEEN -90 AND 90),
    CONSTRAINT chk_customer_addresses_lng CHECK (lng BETWEEN -180 AND 180)
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_0900_ai_ci;


-- -----------------------------------------------------------------------------
-- partners
--
-- Market stall owners who bid on shopping lists. A partner is only broadcast a
-- quote request once status = 'verified', which is what the composite index on
-- (status, market_id) is for.
--
-- NOTE: market_id and category_id are intentionally left as bare nullable
-- columns here. The markets and categories tables arrive in a later migration
-- script, and the foreign keys for both columns must be added in that same
-- later script -- MySQL cannot reference a table that does not exist yet.
-- -----------------------------------------------------------------------------
CREATE TABLE partners (
    id          BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    user_id     BIGINT UNSIGNED NOT NULL,
    stall_name  VARCHAR(150)    NOT NULL,
    market_id   BIGINT UNSIGNED NULL,
    category_id BIGINT UNSIGNED NULL,
    status      VARCHAR(20)     NOT NULL,
    created_at  DATETIME(6)     NOT NULL,
    updated_at  DATETIME(6)     NOT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uq_partners_user (user_id),
    -- Quote broadcast: "verified partners in market X", optionally narrowed by
    -- category. Leading column is status because it is the more selective
    -- filter once the platform has more than a handful of markets.
    KEY idx_partners_status_market (status, market_id),
    KEY idx_partners_category (category_id),
    CONSTRAINT fk_partners_user
        FOREIGN KEY (user_id) REFERENCES users (id)
        ON DELETE CASCADE,
    CONSTRAINT chk_partners_status
        CHECK (status IN ('pending_kyc', 'under_review', 'verified', 'suspended', 'rejected'))
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_0900_ai_ci;


-- -----------------------------------------------------------------------------
-- riders
--
-- Delivery riders. Same KYC lifecycle as partners. Dispatch filters on status,
-- hence the standalone index.
-- -----------------------------------------------------------------------------
CREATE TABLE riders (
    id           BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    user_id      BIGINT UNSIGNED NOT NULL,
    full_name    VARCHAR(150)    NOT NULL,
    vehicle_type VARCHAR(30)     NULL,
    status       VARCHAR(20)     NOT NULL,
    created_at   DATETIME(6)     NOT NULL,
    updated_at   DATETIME(6)     NOT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uq_riders_user (user_id),
    KEY idx_riders_status (status),
    CONSTRAINT fk_riders_user
        FOREIGN KEY (user_id) REFERENCES users (id)
        ON DELETE CASCADE,
    CONSTRAINT chk_riders_status
        CHECK (status IN ('pending_kyc', 'under_review', 'verified', 'suspended', 'rejected'))
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_0900_ai_ci;
