using System.Fabric;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using static Grid.IGridService;

namespace Grid
{
    public interface IGridService : IService
    {
        public class GridColors
        {
            public required Dictionary<string, string> Colors { get; set; }
            public required long LastUpdate { get; set; }
        }
        Task<GridColors> GetGridAsync();  // Get the full grid
        Task<GridColors> GetUpdatedCellsAsync(long unixTimeSeconds);  // Get only updated cells since last update
        Task<bool> UpdateCellAsync(int x, int y, string color);  // Update a specific cell
    }
    /// <summary>
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>
    internal sealed class Grid : StatefulService, IGridService
    {
        public Grid(StatefulServiceContext context)
            : base(context)
        { }

        private const int GridSizeX = 200;
        private const int GridSizeY = 100;
        private const long UpdateRetentionSeconds = 5 * 60;  // Keep recent changes for 5 minutes

        /// <summary>
        /// Optional override to create listeners (e.g., HTTP, Service Remoting, WCF, etc.) for this service replica to handle client or user requests.
        /// </summary>
        /// <remarks>
        /// For more information on service communication, see https://aka.ms/servicefabricservicecommunication
        /// </remarks>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            return new[]
            {
                new ServiceReplicaListener(context =>
                    new FabricTransportServiceRemotingListener(context, this))
            };
        }

        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            var grid = await StateManager.GetOrAddAsync<IReliableDictionary<(int, int), string>>("grid");

            // Initialize grid if not already done
            for (int x = 0; x < GridSizeX; x++)
            {
                using (var tx = StateManager.CreateTransaction())
                {
                    for (int y = 0; y < GridSizeY; y++)
                    {
                        var key = (x, y);
                        var exists = await grid.ContainsKeyAsync(tx, key);
                        if (!exists)
                        {
                            var color = "#FFFFFF";  // Initialize all cells as white
                            await grid.AddAsync(tx, key, color);
                        }
                    }
                    await tx.CommitAsync();
                }
            }

            // Periodically remove old changes from the change log
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var reliableLong = await StateManager.GetOrAddAsync<IReliableDictionary<long, long>>("reliableLong");
                using (var tx = StateManager.CreateTransaction())
                {
                    var timeToDelete = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - UpdateRetentionSeconds;
                    var lastUpdate = await reliableLong.GetOrAddAsync(tx, 1, timeToDelete);
                    if (lastUpdate < timeToDelete)
                    {
                        var changeLog = await StateManager.GetOrAddAsync<IReliableDictionary<long, List<(int, int)>>>("changeLog");
                        for (long i = lastUpdate; i < timeToDelete; i++)
                        {
                            // Remove the key i
                            await changeLog.TryRemoveAsync(tx, i);
                        }
                    }
                    await reliableLong.SetAsync(tx, 1, timeToDelete);
                    await tx.CommitAsync();
                }

                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }

        // Get the full grid
        public async Task<GridColors> GetGridAsync()
        {
            var result = new GridColors { Colors = new Dictionary<string, string>(), LastUpdate = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
            var grid = await StateManager.GetOrAddAsync<IReliableDictionary<(int, int), string>>("grid");

            using (var tx = StateManager.CreateTransaction())
            {
                var allItems = await grid.CreateEnumerableAsync(tx);
                using (var enumerator = allItems.GetAsyncEnumerator())
                {
                    while (await enumerator.MoveNextAsync(CancellationToken.None))
                    {
                        string xyString = enumerator.Current.Key.Item1 + "," + enumerator.Current.Key.Item2;
                        result.Colors[xyString] = enumerator.Current.Value;
                    }
                }
            }

            return result;
        }

        // Get only the updated cells since the last update timestamp
        public async Task<GridColors> GetUpdatedCellsAsync(long lastUpdateTime)
        {
            // Get the current timestamp
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            // Return the entire grid if the last update is older than retention time
            if ((now - lastUpdateTime) >= UpdateRetentionSeconds)
            {
                return await GetGridAsync();  // Return full grid
            }
            var result = new GridColors { Colors = new Dictionary<string, string>(), LastUpdate = now };
            var grid = await StateManager.GetOrAddAsync<IReliableDictionary<(int, int), string>>("grid");
            var changeLog = await StateManager.GetOrAddAsync<IReliableDictionary<long, List<(int, int)>>>("changeLog");
            using (var tx = StateManager.CreateTransaction())
            {
                for (long updates = lastUpdateTime; updates < now; updates++)
                {
                    // get the changes for the second = update
                    var changes = await changeLog.TryGetValueAsync(tx, updates);
                    if (changes.HasValue)
                    {
                        foreach (var change in changes.Value)
                        {
                            var color = await grid.TryGetValueAsync(tx, change);
                            if (color.HasValue)
                            {
                                string xyString = change.Item1 + "," + change.Item2;
                                result.Colors[xyString] = color.Value;
                            }
                        }
                    }
                }
            }
            return result;
        }

        // Update a specific cell and record the change in the change log
        public async Task<bool> UpdateCellAsync(int x, int y, string color)
        {
            if (x < 0 || x >= GridSizeX || y < 0 || y >= GridSizeY)
            {
                return false;  // Cell out of bounds
            }
            // Test that color is a valid hex color code
            if (!System.Text.RegularExpressions.Regex.IsMatch(color, "^#[0-9A-Fa-f]{6}$"))
            {
                return false;  // Invalid color
            }

            var grid = await StateManager.GetOrAddAsync<IReliableDictionary<(int, int), string>>("grid");
            var changeLog = await StateManager.GetOrAddAsync<IReliableDictionary<long, List<(int, int)>>>("changeLog");

            using (var tx = StateManager.CreateTransaction())
            {
                // Update the grid
                await grid.SetAsync(tx, (x, y), color);
                await tx.CommitAsync();
            }

            // Add the change to the change log with the current timestamp
            using (var tx = StateManager.CreateTransaction())
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var changes = await changeLog.GetOrAddAsync(tx, now, new List<(int, int)>());
                changes.Add((x, y));
                await changeLog.SetAsync(tx, now, changes);
                await tx.CommitAsync();
            }
            return true;
        }
    }
}
