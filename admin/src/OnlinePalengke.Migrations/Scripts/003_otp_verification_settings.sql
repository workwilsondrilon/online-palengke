-- =============================================================================
-- 003_otp_verification_settings.sql
--
-- A single admin-toggleable switch: whether phone-OTP verification is
-- bypassed. Stopgap for the gap between "OTP flow exists" (Epic 2) and "a
-- real SMS provider is wired up" (m360, not yet integrated) -- with no way
-- to actually receive a text, QA and demos would otherwise be blocked on
-- reading the code out of server logs. See the header remarks on
-- OnlinePalengke.Domain.Identity.OtpVerificationSettings for the full
-- rationale and OtpAuthService.VerifyOtpAsync for what bypass does and does
-- not skip.
--
--   otp_verification_settings
--
-- Conventions: see the header of 001_identity_and_media.sql.
--
-- One addition this script needs that 001/002 didn't: a genuine singleton
-- row. `id` is pinned to 1 by a CHECK rather than left as an
-- AUTO_INCREMENT surrogate key, so the table can never accidentally hold a
-- second row for a second "current" setting. The repository upserts against
-- id = 1 and treats a missing row (nothing has ever toggled this yet) as
-- bypass off, so this script does not need to seed one.
-- =============================================================================


-- -----------------------------------------------------------------------------
-- otp_verification_settings
-- -----------------------------------------------------------------------------
CREATE TABLE otp_verification_settings (
    id             TINYINT UNSIGNED NOT NULL,
    bypass_enabled BOOLEAN          NOT NULL DEFAULT FALSE,
    updated_at     DATETIME(6)      NOT NULL,
    PRIMARY KEY (id),
    CONSTRAINT chk_otp_verification_settings_singleton CHECK (id = 1)
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_0900_ai_ci;
