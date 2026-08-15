using Amazon;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OnlinePalengke.Application.Abstractions;
using OnlinePalengke.Infrastructure.Auth;
using OnlinePalengke.Infrastructure.Persistence;
using OnlinePalengke.Infrastructure.Persistence.Repositories;
using OnlinePalengke.Infrastructure.Storage;

namespace OnlinePalengke.Infrastructure;

/// <summary>Registers database access, object storage, auth primitives and the clock.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        DapperConfiguration.Apply();

        AddDatabase(services, configuration);
        AddStorage(services, configuration);
        AddAuth(services, configuration);

        services.AddSingleton<IClock, SystemClock>();

        return services;
    }

    private static void AddDatabase(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateDataAnnotations()
            // Validating on start turns a missing connection string into a clear failure
            // at boot rather than a confusing null-reference on the first request.
            .ValidateOnStart();

        services.AddSingleton<IDbConnectionFactory, MySqlConnectionFactory>();

        // Scoped, so one connection serves the whole request and every repository in it
        // participates in the same ambient transaction.
        services.AddScoped<DbSession>();
        services.AddScoped<IDbSession>(sp => sp.GetRequiredService<DbSession>());
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IMediaAssetRepository, MediaAssetRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IOtpCodeRepository, OtpCodeRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IUnitRepository, UnitRepository>();
        services.AddScoped<IItemRepository, ItemRepository>();
        services.AddScoped<IItemUnitRepository, ItemUnitRepository>();

        services.AddScoped<IMarketRepository, MarketRepository>();
        services.AddScoped<IDeliveryWindowRepository, DeliveryWindowRepository>();
    }

    private static void AddAuth(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();

        // The only ISmsSender today - see LoggingSmsSender's remarks. Swap this one line
        // for a real Semaphore/Movider adapter when one is built; nothing else changes.
        services.AddSingleton<ISmsSender, LoggingSmsSender>();
    }

    private static void AddStorage(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<S3Options>()
            .Bind(configuration.GetSection(S3Options.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IAmazonS3>(sp =>
        {
            var options = configuration.GetSection(S3Options.SectionName).Get<S3Options>()
                ?? throw new InvalidOperationException(
                    $"The '{S3Options.SectionName}' configuration section is missing.");

            // Credentials come from the default AWS chain — environment variables and the
            // shared profile locally, an IAM role when deployed. Never from configuration.
            return new AmazonS3Client(new AmazonS3Config
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region),
            });
        });

        services.AddSingleton<IFileStorage, S3FileStorage>();
    }
}
