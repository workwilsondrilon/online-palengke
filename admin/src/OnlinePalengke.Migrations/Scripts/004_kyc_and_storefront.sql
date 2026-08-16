-- =============================================================================
-- 004_kyc_and_storefront.sql
--
-- Epic 4: configurable KYC document types, the documents partners/riders
-- submit against them, partner product declarations (the storefront), and
-- content moderation reports.
--
--   document_types    -- admin-configurable, per role + category
--   kyc_documents      -- one row per submission (resubmission inserts a new
--                          row rather than mutating a rejected one, so review
--                          history is never lost)
--   partner_products   -- "this stall carries this item", plus optional
--                          marketing (image/headline/description)
--   content_reports    -- flags against published content
--
-- Conventions: see the header of 001_identity_and_media.sql and
-- README.Database.md section 4 -- all still apply here (snake_case, BIGINT
-- UNSIGNED surrogate keys, DATETIME(6) UTC timestamps written explicitly,
-- VARCHAR + named CHECK instead of ENUM, no ON UPDATE CURRENT_TIMESTAMP).
--
-- `partners`/`riders` themselves are untouched by this script -- migration
-- 001 already has the right shape (status CHECK, nullable market_id/category_id).
-- =============================================================================


-- -----------------------------------------------------------------------------
-- document_types
--
-- Admin-configurable KYC requirements. applies_to_category_id is NULL for a
-- role-wide requirement (e.g. every partner needs a valid ID) and set for a
-- category-specific add-on (e.g. Meat/Poultry/Fish also need a health card).
-- A partner/rider is 'verified' only once every is_required row matching
-- their role and category has an approved, unexpired kyc_documents row --
-- see KycDocumentService (Application layer, task #36).
-- -----------------------------------------------------------------------------
CREATE TABLE document_types (
    id                     BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    code                   VARCHAR(64)     NOT NULL,
    name                   VARCHAR(150)    NOT NULL,
    applies_to_role        VARCHAR(20)     NOT NULL,
    applies_to_category_id BIGINT UNSIGNED NULL,
    is_required            BOOLEAN         NOT NULL DEFAULT TRUE,
    requires_expiry        BOOLEAN         NOT NULL DEFAULT FALSE,
    created_at             DATETIME(6)     NOT NULL,
    updated_at             DATETIME(6)     NOT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uq_document_types_code (code),
    KEY idx_document_types_role_category (applies_to_role, applies_to_category_id),
    CONSTRAINT fk_document_types_category
        FOREIGN KEY (applies_to_category_id) REFERENCES categories (id)
        ON DELETE RESTRICT,
    CONSTRAINT chk_document_types_role
        CHECK (applies_to_role IN ('partner', 'rider'))
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_0900_ai_ci;


-- -----------------------------------------------------------------------------
-- kyc_documents
--
-- One row per submission. A rejection is never overwritten by a resubmission
-- -- the owner uploads again and a new row is inserted, so the rejection
-- reason and reviewer stay on the historical record. Completeness checks
-- always read the newest row per (owner_role, owner_id, document_type_id),
-- which idx_kyc_documents_owner serves. media_asset_id is ON DELETE RESTRICT,
-- matching media_assets.uploaded_by's own RESTRICT: a KYC document is
-- evidence and must not silently lose its backing file.
-- -----------------------------------------------------------------------------
CREATE TABLE kyc_documents (
    id                BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    owner_role        VARCHAR(20)     NOT NULL,
    owner_id          BIGINT UNSIGNED NOT NULL,
    document_type_id  BIGINT UNSIGNED NOT NULL,
    media_asset_id    BIGINT UNSIGNED NOT NULL,
    status            VARCHAR(20)     NOT NULL,
    submitted_at      DATETIME(6)     NOT NULL,
    reviewed_by       BIGINT UNSIGNED NULL,
    reviewed_at       DATETIME(6)     NULL,
    rejection_reason  VARCHAR(1000)   NULL,
    expires_at        DATETIME(6)     NULL,
    PRIMARY KEY (id),
    KEY idx_kyc_documents_owner (owner_role, owner_id, document_type_id),
    KEY idx_kyc_documents_status (status),
    CONSTRAINT fk_kyc_documents_type
        FOREIGN KEY (document_type_id) REFERENCES document_types (id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_kyc_documents_media
        FOREIGN KEY (media_asset_id) REFERENCES media_assets (id)
        ON DELETE RESTRICT,
    CONSTRAINT chk_kyc_documents_owner_role
        CHECK (owner_role IN ('partner', 'rider')),
    CONSTRAINT chk_kyc_documents_status
        CHECK (status IN ('submitted', 'approved', 'rejected', 'expired'))
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_0900_ai_ci;


-- -----------------------------------------------------------------------------
-- partner_products
--
-- "This stall carries this item" -- the row's existence drives Epic 5's
-- quote-request matching regardless of publish state. media_asset_id/
-- headline/description are the marketing layer on top: publish-on-save per
-- decision 19, no pre-approval gate. unpublished_by/unpublished_reason are
-- set on an admin takedown (ContentModerationService) or the partner's own
-- unpublish action; a takedown never deletes the row or suspends the
-- partner, matching decision 19's explicit scope.
-- -----------------------------------------------------------------------------
CREATE TABLE partner_products (
    id                 BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    partner_id         BIGINT UNSIGNED NOT NULL,
    item_id            BIGINT UNSIGNED NOT NULL,
    media_asset_id     BIGINT UNSIGNED NULL,
    headline           VARCHAR(150)    NOT NULL,
    description        VARCHAR(2000)   NULL,
    is_published       BOOLEAN         NOT NULL DEFAULT TRUE,
    unpublished_by     BIGINT UNSIGNED NULL,
    unpublished_reason VARCHAR(1000)   NULL,
    created_at         DATETIME(6)     NOT NULL,
    updated_at         DATETIME(6)     NOT NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uq_partner_products_partner_item (partner_id, item_id),
    KEY idx_partner_products_item (item_id),
    CONSTRAINT fk_partner_products_partner
        FOREIGN KEY (partner_id) REFERENCES partners (id)
        ON DELETE CASCADE,
    CONSTRAINT fk_partner_products_item
        FOREIGN KEY (item_id) REFERENCES items (id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_partner_products_media
        FOREIGN KEY (media_asset_id) REFERENCES media_assets (id)
        ON DELETE SET NULL
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_0900_ai_ci;


-- -----------------------------------------------------------------------------
-- content_reports
--
-- Flags against published content, reviewed by an admin. target_type is
-- restricted to 'partner_product' for now -- the only reportable content
-- type this epic introduces -- but is a VARCHAR + CHECK specifically so a
-- later content type is a constraint swap, not a table rebuild.
-- -----------------------------------------------------------------------------
CREATE TABLE content_reports (
    id               BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    reporter_user_id BIGINT UNSIGNED NOT NULL,
    target_type      VARCHAR(30)     NOT NULL,
    target_id        BIGINT UNSIGNED NOT NULL,
    reason           VARCHAR(1000)   NOT NULL,
    status           VARCHAR(20)     NOT NULL,
    resolved_by      BIGINT UNSIGNED NULL,
    resolved_at      DATETIME(6)     NULL,
    created_at       DATETIME(6)     NOT NULL,
    PRIMARY KEY (id),
    KEY idx_content_reports_status (status),
    KEY idx_content_reports_target (target_type, target_id),
    CONSTRAINT fk_content_reports_reporter
        FOREIGN KEY (reporter_user_id) REFERENCES users (id)
        ON DELETE CASCADE,
    CONSTRAINT chk_content_reports_target_type
        CHECK (target_type IN ('partner_product')),
    CONSTRAINT chk_content_reports_status
        CHECK (status IN ('open', 'resolved', 'dismissed'))
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_0900_ai_ci;


-- -----------------------------------------------------------------------------
-- Seed: document_types
--
-- From the plan's KYC section. Role-wide requirements apply to every
-- category (applies_to_category_id NULL); Meat/Poultry/Fish & Seafood
-- additionally require a sanitary permit and health card, looked up by
-- category name below. If none of those names match a seeded categories row
-- (e.g. on a fresh database where no category has been created yet), the
-- INSERT ... SELECT simply inserts nothing -- no hard failure -- so confirm
-- row counts after running this against a database you expect to already
-- have categories in.
-- -----------------------------------------------------------------------------
INSERT INTO document_types (code, name, applies_to_role, applies_to_category_id, is_required, requires_expiry, created_at, updated_at)
VALUES
    ('partner_valid_id', 'Valid government ID', 'partner', NULL, TRUE, TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('partner_business_permit', 'Mayor''s / business permit', 'partner', NULL, TRUE, TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('partner_barangay_clearance', 'Barangay clearance', 'partner', NULL, TRUE, TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('rider_drivers_licence', 'Driver''s licence', 'rider', NULL, TRUE, TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('rider_or_cr', 'OR/CR', 'rider', NULL, TRUE, TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)),
    ('rider_nbi_clearance', 'NBI clearance', 'rider', NULL, TRUE, TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6));

INSERT INTO document_types (code, name, applies_to_role, applies_to_category_id, is_required, requires_expiry, created_at, updated_at)
SELECT 'partner_sanitary_permit', 'Sanitary permit', 'partner', id, TRUE, TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
FROM categories WHERE name IN ('Meat', 'Poultry', 'Fish & Seafood');

INSERT INTO document_types (code, name, applies_to_role, applies_to_category_id, is_required, requires_expiry, created_at, updated_at)
SELECT 'partner_health_card', 'Health card', 'partner', id, TRUE, TRUE, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
FROM categories WHERE name IN ('Meat', 'Poultry', 'Fish & Seafood');
