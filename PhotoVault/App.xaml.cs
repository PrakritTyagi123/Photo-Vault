using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PhotoVault.Core.Data;
using PhotoVault.Services;
using PhotoVault.ViewModels;

namespace PhotoVault;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PhotoVault");
        var thumbDir = Path.Combine(appData, "thumbnails");
        var vaultDir = Path.Combine(appData, "vault");
        var pluginDir = Path.Combine(appData, "plugins");
        var backupDir = Path.Combine(appData, "backups");
        var dbPath = Path.Combine(appData, "photovault.db");

        Directory.CreateDirectory(appData);
        Directory.CreateDirectory(thumbDir);
        Directory.CreateDirectory(vaultDir);
        Directory.CreateDirectory(pluginDir);
        Directory.CreateDirectory(backupDir);

        var db = new DatabaseService(dbPath);
        db.Initialize();

        var logService = new LogService();
        var mediaRepo = new MediaRepository(db);
        var albumRepo = new AlbumRepository(db);
        var thumbService = new ThumbnailService(db, thumbDir);
        var exifService = new ExifService(db);
        var searchService = new SearchService(db);
        var settingsService = new SettingsService(db, logService);
        var videoService = new VideoService(db, thumbDir, logService);
        var dupService = new DuplicateDetectionService(db, logService);
        var geoService = new GeocodingService(db, logService);
        var qualityService = new QualityScoringService(db, logService);
        var insightsService = new InsightsService(db);
        var healthService = new HealthService(db, logService);
        var cleanupService = new CleanupService(db, dupService, logService);
        var tripService = new TripDetectionService(db, logService);
        var vaultService = new VaultService(db, logService, vaultDir);
        var exportService = new ExportService(db, albumRepo, logService);
        var backupService = new BackupService(db, logService, dbPath, thumbDir);
        var mapService = new MapService(db);
        var pluginService = new PluginService(logService, pluginDir);
        var aiService = new AiSidecarService(logService);
        var scanPipeline = new ScanPipelineService(db, thumbService, exifService, searchService,
            videoService, dupService, geoService, qualityService, aiService, logService);

        settingsService.EnsureDefaults();
        pluginService.LoadPlugins();

        var services = new ServiceCollection();

        services.AddSingleton(db); services.AddSingleton(mediaRepo); services.AddSingleton(albumRepo);
        services.AddSingleton(logService); services.AddSingleton(thumbService); services.AddSingleton(exifService);
        services.AddSingleton(searchService); services.AddSingleton(settingsService); services.AddSingleton(videoService);
        services.AddSingleton(dupService); services.AddSingleton(geoService); services.AddSingleton(qualityService);
        services.AddSingleton(insightsService); services.AddSingleton(healthService); services.AddSingleton(cleanupService);
        services.AddSingleton(tripService); services.AddSingleton(vaultService); services.AddSingleton(exportService);
        services.AddSingleton(backupService); services.AddSingleton(mapService); services.AddSingleton(pluginService);
        services.AddSingleton(aiService); services.AddSingleton(scanPipeline);
        services.AddSingleton<FileDiscoveryService>();

        services.AddSingleton<ViewerViewModel>(); services.AddSingleton<SearchViewModel>();
        services.AddSingleton<AlbumViewModel>(); services.AddSingleton<ScanCenterViewModel>();
        services.AddSingleton<SettingsViewModel>(); services.AddSingleton<LogViewModel>();
        services.AddSingleton<HealthViewModel>(); services.AddSingleton<InsightsViewModel>();
        services.AddSingleton<CleanupViewModel>(); services.AddSingleton<TripsViewModel>();
        services.AddSingleton<VaultViewModel>(); services.AddSingleton<ExportViewModel>();
        services.AddSingleton<BackupViewModel>(); services.AddSingleton<PluginsViewModel>();
        services.AddSingleton<MapViewModel>(); services.AddSingleton<PeopleViewModel>();
        services.AddSingleton<GalleryViewModel>(); services.AddSingleton<MainViewModel>();

        Services = services.BuildServiceProvider();

        logService.Info("App", "PhotoVault started");
        logService.Info("App", $"Database: {dbPath}");
        logService.Info("App", $"Library: {mediaRepo.GetCount()} items");
    }
}
