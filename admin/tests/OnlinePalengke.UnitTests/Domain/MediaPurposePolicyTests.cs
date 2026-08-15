using OnlinePalengke.Domain.Identity;
using OnlinePalengke.Domain.Media;

namespace OnlinePalengke.UnitTests.Domain;

/// <summary>
/// <see cref="MediaPurposePolicy"/> is a security boundary, not a convenience. These
/// tests assert the two properties that matter most: a government ID can never be made
/// publicly readable, and no role can upload for a purpose it has no business touching.
/// </summary>
public sealed class MediaPurposePolicyTests
{
    private const long OneMegabyte = 1024L * 1024L;

    [Fact]
    public void Every_declared_purpose_has_a_rule()
    {
        // Without this, adding an enum member and forgetting the rule would throw at
        // runtime on the first upload rather than failing here.
        foreach (var purpose in Enum.GetValues<MediaPurpose>())
        {
            Assert.Contains(purpose, MediaPurposePolicy.AllPurposes);
            Assert.NotNull(MediaPurposePolicy.For(purpose));
        }
    }

    [Theory]
    [InlineData(MediaPurpose.KycDocument)]
    [InlineData(MediaPurpose.DeliveryProof)]
    public void Sensitive_purposes_are_private(MediaPurpose purpose) =>
        Assert.Equal(MediaVisibility.Private, MediaPurposePolicy.For(purpose).Visibility);

    [Theory]
    [InlineData(MediaPurpose.PartnerProduct)]
    [InlineData(MediaPurpose.ItemImage)]
    [InlineData(MediaPurpose.ProfilePhoto)]
    public void Marketing_and_catalog_purposes_are_public(MediaPurpose purpose) =>
        Assert.Equal(MediaVisibility.Public, MediaPurposePolicy.For(purpose).Visibility);

    [Fact]
    public void A_customer_cannot_upload_a_kyc_document()
    {
        // Customers never submit KYC — only partners and riders do. A customer asking to
        // write into the private bucket is either a client bug or someone probing.
        var refusal = MediaPurposePolicy.Validate(
            MediaPurpose.KycDocument, UserRole.Customer, "application/pdf", OneMegabyte);

        Assert.NotNull(refusal);
    }

    [Fact]
    public void A_customer_cannot_upload_partner_marketing_content()
    {
        var refusal = MediaPurposePolicy.Validate(
            MediaPurpose.PartnerProduct, UserRole.Customer, "image/jpeg", OneMegabyte);

        Assert.NotNull(refusal);
    }

    [Fact]
    public void Only_admin_may_upload_item_master_images()
    {
        // The item master is admin-curated. A partner writing to it would be editing the
        // shared catalog every other stall quotes against.
        foreach (var role in Enum.GetValues<UserRole>())
        {
            var refusal = MediaPurposePolicy.Validate(
                MediaPurpose.ItemImage, role, "image/jpeg", OneMegabyte);

            if (role == UserRole.Admin)
            {
                Assert.Null(refusal);
            }
            else
            {
                Assert.NotNull(refusal);
            }
        }
    }

    [Fact]
    public void Only_a_rider_may_upload_delivery_proof()
    {
        foreach (var role in Enum.GetValues<UserRole>())
        {
            var refusal = MediaPurposePolicy.Validate(
                MediaPurpose.DeliveryProof, role, "image/jpeg", OneMegabyte);

            Assert.Equal(role == UserRole.Rider, refusal is null);
        }
    }

    [Theory]
    [InlineData(UserRole.Partner)]
    [InlineData(UserRole.Rider)]
    public void Partners_and_riders_may_submit_kyc_documents(UserRole role) =>
        Assert.Null(MediaPurposePolicy.Validate(
            MediaPurpose.KycDocument, role, "application/pdf", 5 * OneMegabyte));

    [Fact]
    public void Every_role_may_upload_a_profile_photo()
    {
        foreach (var role in Enum.GetValues<UserRole>())
        {
            Assert.Null(MediaPurposePolicy.Validate(
                MediaPurpose.ProfilePhoto, role, "image/jpeg", OneMegabyte));
        }
    }

    [Fact]
    public void Pdf_is_accepted_for_kyc_but_not_for_marketing()
    {
        // Permits are routinely scanned as multi-page PDFs; a product photo is not.
        Assert.Null(MediaPurposePolicy.Validate(
            MediaPurpose.KycDocument, UserRole.Partner, "application/pdf", OneMegabyte));

        Assert.NotNull(MediaPurposePolicy.Validate(
            MediaPurpose.PartnerProduct, UserRole.Partner, "application/pdf", OneMegabyte));
    }

    [Fact]
    public void An_executable_is_never_accepted()
    {
        foreach (var purpose in Enum.GetValues<MediaPurpose>())
        {
            foreach (var role in MediaPurposePolicy.For(purpose).AllowedRoles)
            {
                Assert.NotNull(MediaPurposePolicy.Validate(
                    purpose, role, "application/x-msdownload", OneMegabyte));
            }
        }
    }

    [Fact]
    public void Content_type_matching_ignores_case_and_surrounding_whitespace() =>
        Assert.Null(MediaPurposePolicy.Validate(
            MediaPurpose.ProfilePhoto, UserRole.Customer, "  IMAGE/JPEG  ", OneMegabyte));

    [Fact]
    public void Content_type_is_matched_exactly_not_by_prefix() =>
        Assert.NotNull(MediaPurposePolicy.Validate(
            MediaPurpose.ProfilePhoto, UserRole.Customer, "image/jpeg; charset=binary", OneMegabyte));

    [Fact]
    public void A_size_at_the_limit_is_accepted_and_one_byte_over_is_not()
    {
        var rule = MediaPurposePolicy.For(MediaPurpose.ProfilePhoto);

        Assert.Null(MediaPurposePolicy.Validate(
            MediaPurpose.ProfilePhoto, UserRole.Customer, "image/jpeg", rule.MaxSizeBytes));

        Assert.NotNull(MediaPurposePolicy.Validate(
            MediaPurpose.ProfilePhoto, UserRole.Customer, "image/jpeg", rule.MaxSizeBytes + 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_non_positive_size_is_rejected(long sizeBytes) =>
        Assert.NotNull(MediaPurposePolicy.Validate(
            MediaPurpose.ProfilePhoto, UserRole.Customer, "image/jpeg", sizeBytes));

    [Fact]
    public void Kyc_documents_get_more_size_headroom_than_photos()
    {
        // Scanned permits are large and often badly photographed; a rejected upload at
        // 4 AM in a wet market is a real onboarding failure.
        Assert.True(
            MediaPurposePolicy.For(MediaPurpose.KycDocument).MaxSizeBytes
            > MediaPurposePolicy.For(MediaPurpose.ProfilePhoto).MaxSizeBytes);
    }
}
