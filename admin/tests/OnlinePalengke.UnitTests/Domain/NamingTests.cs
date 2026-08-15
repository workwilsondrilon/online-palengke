using OnlinePalengke.Domain.Common;
using OnlinePalengke.Domain.Identity;
using OnlinePalengke.Domain.Media;

namespace OnlinePalengke.UnitTests.Domain;

/// <summary>
/// <see cref="Naming"/> defines the persisted data format — both column matching and the
/// string values written into the VARCHAR columns that stand in for enums. A change here
/// silently changes what is written to the database, so it is covered thoroughly.
/// </summary>
public sealed class NamingTests
{
    [Theory]
    [InlineData("CreatedAt", "created_at")]
    [InlineData("SizeBytes", "size_bytes")]
    [InlineData("Id", "id")]
    [InlineData("PendingKyc", "pending_kyc")]
    [InlineData("KycDocument", "kyc_document")]
    [InlineData("ObjectKey", "object_key")]
    [InlineData("UnderReview", "under_review")]
    [InlineData("Customer", "customer")]
    [InlineData("PartnerProduct", "partner_product")]
    public void ToSnakeCase_converts_pascal_case(string input, string expected) =>
        Assert.Equal(expected, Naming.ToSnakeCase(input));

    [Fact]
    public void ToSnakeCase_keeps_runs_of_capitals_together()
    {
        // "HTTPServer" is one acronym followed by a word, not eight separate words.
        Assert.Equal("http_server", Naming.ToSnakeCase("HTTPServer"));
        Assert.Equal("kyc", Naming.ToSnakeCase("KYC"));
    }

    [Fact]
    public void ToSnakeCase_does_not_double_underscores() =>
        Assert.Equal("already_snake", Naming.ToSnakeCase("already_snake"));

    [Theory]
    [InlineData("created_at", "CreatedAt")]
    [InlineData("CreatedAt", "CreatedAt")]
    [InlineData("CREATED_AT", "CreatedAt")]
    public void ColumnMatchesMember_accepts_either_convention(string column, string member) =>
        Assert.True(Naming.ColumnMatchesMember(column, member));

    [Fact]
    public void ColumnMatchesMember_rejects_a_genuine_mismatch() =>
        Assert.False(Naming.ColumnMatchesMember("created_at", "UpdatedAt"));

    [Fact]
    public void Every_UserRole_round_trips_through_the_database_form() =>
        AssertRoundTrips<UserRole>();

    [Fact]
    public void Every_PartnerStatus_round_trips_through_the_database_form() =>
        AssertRoundTrips<PartnerStatus>();

    [Fact]
    public void Every_RiderStatus_round_trips_through_the_database_form() =>
        AssertRoundTrips<RiderStatus>();

    [Fact]
    public void Every_MediaPurpose_round_trips_through_the_database_form() =>
        AssertRoundTrips<MediaPurpose>();

    [Fact]
    public void Every_MediaAssetState_round_trips_through_the_database_form() =>
        AssertRoundTrips<MediaAssetState>();

    [Fact]
    public void FromDbValue_is_case_insensitive() =>
        Assert.Equal(PartnerStatus.PendingKyc, Naming.FromDbValue<PartnerStatus>("PENDING_KYC"));

    [Fact]
    public void FromDbValue_throws_on_an_unknown_value()
    {
        // Falling back to the zero member would silently treat an unrecognised partner
        // state as "pending_kyc", which is exactly the kind of bug that never gets found.
        var ex = Assert.Throws<InvalidOperationException>(
            () => Naming.FromDbValue<PartnerStatus>("probationary"));

        Assert.Contains("probationary", ex.Message, StringComparison.Ordinal);
        Assert.Contains("migration", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Guards the schema's CHECK constraints: every enum member must survive a trip
    /// through its database representation and back.
    /// </summary>
    private static void AssertRoundTrips<TEnum>()
        where TEnum : struct, Enum
    {
        foreach (var value in Enum.GetValues<TEnum>())
        {
            var dbValue = Naming.ToDbValue(value);

            Assert.Equal(dbValue, dbValue.ToLowerInvariant());
            Assert.False(dbValue.Contains(' ', StringComparison.Ordinal), $"'{dbValue}' contains a space.");
            Assert.Equal(value, Naming.FromDbValue<TEnum>(dbValue));
        }
    }
}
