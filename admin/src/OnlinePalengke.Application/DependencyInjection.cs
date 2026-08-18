using Microsoft.Extensions.DependencyInjection;
using OnlinePalengke.Application.Auth;
using OnlinePalengke.Application.Catalog;
using OnlinePalengke.Application.Kyc;
using OnlinePalengke.Application.Markets;
using OnlinePalengke.Application.Moderation;
using OnlinePalengke.Application.Onboarding;
using OnlinePalengke.Application.Storefront;
using OnlinePalengke.Application.Uploads;

namespace OnlinePalengke.Application;

/// <summary>Registers the application's use-case services.</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds every application service. Infrastructure concerns — the database, S3, the
    /// clock — are registered separately by <c>AddInfrastructure</c>; this layer only
    /// declares the interfaces it needs.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<UploadService>();

        services.AddScoped<AuthSessionFactory>();
        services.AddScoped<OtpAuthService>();
        services.AddScoped<AdminAuthService>();
        services.AddScoped<OtpSettingsService>();

        services.AddScoped<CategoryService>();
        services.AddScoped<UnitService>();
        services.AddScoped<ItemService>();

        services.AddScoped<MarketService>();
        services.AddScoped<DeliveryWindowService>();
        services.AddScoped<MarketEligibilityService>();

        services.AddScoped<PartnerService>();
        services.AddScoped<RiderService>();
        services.AddScoped<DocumentTypeService>();
        services.AddScoped<KycDocumentService>();
        services.AddScoped<PartnerProductService>();
        services.AddScoped<ContentModerationService>();

        return services;
    }
}
