using System.Threading.Channels;
using Aspire.V1;
using Grpc.Core;

namespace ResourceService.Services
{
    public class DashboardServiceImpl : DashboardService.DashboardServiceBase
    {
        private static readonly Channel<AspireResource> _channel;

        static DashboardServiceImpl()
        {
            _channel = Channel.CreateUnbounded<AspireResource>();
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(3000);
                    await _channel.Writer.WriteAsync(new AspireResource { Name = Guid.NewGuid().ToString() });
                }
            });
        }

        public override async Task WatchResources(
            WatchResourcesRequest request,
            IServerStreamWriter<WatchResourcesResponse> responseStream,
            ServerCallContext context)
        {
            // It will be up to the server to have thread safety and prevent
            // a race between building the initial list and starting streaming.
            var snapshotResponse = new WatchResourcesResponse
            {
                InitialSnapshot = new WatchResourcesSnapshot()
            };
            snapshotResponse.InitialSnapshot.Items.Add(new Resource { Name = "One" });
            snapshotResponse.InitialSnapshot.Items.Add(new Resource { Name = "Two" });

            await responseStream.WriteAsync(snapshotResponse);

            await foreach (var resource in _channel.Reader.ReadAllAsync(context.CancellationToken))
            {
                var changeResponse = new WatchResourcesResponse
                {
                    Change = new WatchResourcesChange
                    {
                        Resource = new Resource
                        {
                            Name = resource.Name,
                        }
                    }
                };

                await responseStream.WriteAsync(changeResponse);
            }
        }
    }

    public class AspireResource
    {
        public string? Name { get; set; }
    }
}
