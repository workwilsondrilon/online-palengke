namespace OnlinePalengke.Domain.Moderation;

/// <summary>
/// What kind of content a <see cref="ContentReport"/> points at.
/// <see cref="OnlinePalengke.Domain.Media.PartnerProduct"/> is the only reportable content
/// type this epic introduces; the underlying column is a CHECK-constrained VARCHAR
/// specifically so a later type is a constraint swap, not a table rebuild — see
/// <c>ContentReport.TargetId</c>.
/// </summary>
public enum ContentTargetType
{
    PartnerProduct,
}
