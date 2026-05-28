using DoAn.HotelParking.Core.Application.Interfaces.Base;
using DoAn.HotelParking.Core.Application.Interfaces.Auth;
using DoAn.HotelParking.Core.Application.Interfaces.Booking;
using DoAn.HotelParking.Core.Application.Interfaces.Hotel;
using DoAn.HotelParking.Core.Application.Interfaces.Location;
using DoAn.HotelParking.Core.Application.Interfaces.Notification;
using DoAn.HotelParking.Core.Application.Interfaces.OwnerSetting;
using DoAn.HotelParking.Core.Application.Interfaces.Payment;
using DoAn.HotelParking.Core.Application.Interfaces.Review;
using DoAn.HotelParking.Core.Application.Interfaces.Role;
using DoAn.HotelParking.Core.Application.Interfaces.Room;
using DoAn.HotelParking.Core.Application.Interfaces.RoomType;
using DoAn.HotelParking.Core.Application.Interfaces.Storage;
using DoAn.HotelParking.Core.Application.Interfaces.SystemConfig;
using DoAn.HotelParking.Core.Application.Interfaces.TimeSlot;
using DoAn.HotelParking.Core.Application.Interfaces.User;
using DoAn.HotelParking.Infrastructure.Data;
using DoAn.HotelParking.Infrastructure.Authentication;
using DoAn.HotelParking.Infrastructure.Notification;
using DoAn.HotelParking.Infrastructure.Repositories.Base;
using DoAn.HotelParking.Infrastructure.Repositories.Auth;
using DoAn.HotelParking.Infrastructure.Repositories.Booking;
using DoAn.HotelParking.Infrastructure.Repositories.Hotel;
using DoAn.HotelParking.Infrastructure.Repositories.Location;
using DoAn.HotelParking.Infrastructure.Repositories.Notification;
using DoAn.HotelParking.Infrastructure.Repositories.OwnerSetting;
using DoAn.HotelParking.Infrastructure.Repositories.Payment;
using DoAn.HotelParking.Infrastructure.Repositories.Review;
using DoAn.HotelParking.Infrastructure.Repositories.Role;
using DoAn.HotelParking.Infrastructure.Repositories.Room;
using DoAn.HotelParking.Infrastructure.Repositories.RoomType;
using DoAn.HotelParking.Infrastructure.Repositories.SystemConfig;
using DoAn.HotelParking.Infrastructure.Repositories.TimeSlot;
using DoAn.HotelParking.Infrastructure.Repositories.User;
using DoAn.HotelParking.Infrastructure.Storage;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DoAn.HotelParking.Infrastructure;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                sql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null);
            }));

        services.Configure<JwtSettings>(configuration.GetSection("JwtSettings"));
        services.Configure<MinioSettings>(configuration.GetSection("Minio"));
        services.Configure<FirebaseSettings>(configuration.GetSection("Firebase"));

        services.AddSingleton(sp =>
        {
            const string appName = "HotelParkingFcm";

            try
            {
                return FirebaseApp.GetInstance(appName);
            }
            catch (ArgumentException)
            {
                var settings = sp.GetRequiredService<IOptions<FirebaseSettings>>().Value;
                if (string.IsNullOrWhiteSpace(settings.CredentialsPath))
                {
                    throw new InvalidOperationException("Firebase:CredentialsPath is required.");
                }

                var hostEnvironment = sp.GetRequiredService<IHostEnvironment>();
                var resolvedPath = ResolveFirebaseCredentialsPath(settings.CredentialsPath, hostEnvironment.ContentRootPath);

                var appOptions = new AppOptions
                {
                    Credential = GoogleCredential.FromFile(resolvedPath)
                };

                if (!string.IsNullOrWhiteSpace(settings.ProjectId))
                {
                    appOptions.ProjectId = settings.ProjectId;
                }

                return FirebaseApp.Create(appOptions, appName);
            }
        });

        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IUserRoleRepository, UserRoleRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IFcmTokenRepository, FcmTokenRepository>();

        services.AddScoped<IHotelRepository, HotelRepository>();
        services.AddScoped<IFavoriteHotelRepository, FavoriteHotelRepository>();
        services.AddScoped<IHotelImageRepository, HotelImageRepository>();
        services.AddScoped<IRoomRepository, RoomRepository>();
        services.AddScoped<IRoomTypeRepository, RoomTypeRepository>();
        services.AddScoped<ITimeSlotRepository, TimeSlotRepository>();
        services.AddScoped<IProvinceRepository, ProvinceRepository>();
        services.AddScoped<IWardRepository, WardRepository>();
        services.AddScoped<IOwnerSettingRepository, OwnerSettingRepository>();
        services.AddScoped<ISystemConfigRepository, SystemConfigRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<INotificationPushService, FcmNotificationPushService>();

        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IObjectStorageService, MinioObjectStorageService>();
        services.AddScoped<INotificationPushService, FcmNotificationPushService>();

        return services;
    }

    private static string ResolveFirebaseCredentialsPath(string configuredPath, string contentRootPath)
    {
        var candidates = new List<string>();

        if (Path.IsPathRooted(configuredPath))
        {
            candidates.Add(configuredPath);
        }
        else
        {
            candidates.Add(Path.Combine(contentRootPath, configuredPath));
            candidates.Add(Path.Combine(contentRootPath, "..", configuredPath));
            candidates.Add(Path.Combine(contentRootPath, "..", "..", configuredPath));
            candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), configuredPath));
        }

        var normalizedCandidates = candidates
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existingPath = normalizedCandidates.FirstOrDefault(File.Exists);
        if (existingPath is not null)
        {
            return existingPath;
        }

        throw new InvalidOperationException(
            $"Firebase credentials file was not found. Checked paths: {string.Join("; ", normalizedCandidates)}");
    }
}
