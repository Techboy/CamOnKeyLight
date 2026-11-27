using CamOnKeyLight.Core;

namespace CamOnKeyLight.Relay
{
    public sealed class DiscoveryHostedService : BackgroundService
    {
        private readonly LightCatalog catalog;
        private readonly ILogger<DiscoveryHostedService> logger;

        public DiscoveryHostedService(LightCatalog catalog, ILogger<DiscoveryHostedService> logger)
        {
            this.catalog = catalog;
            this.logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    IReadOnlyList<DiscoveredLight> found = await LanDiscovery.QueryAsync(TimeSpan.FromSeconds(2), stoppingToken);
                    int added = 0;
                    foreach (DiscoveredLight light in found)
                    {
                        if (catalog.TryAdd(light.Address))
                            added++;
                    }

                    logger.LogInformation("Light discovery saw {Found} lights and kept {Added}.", found.Count, added);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Light discovery failed.");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }
}
