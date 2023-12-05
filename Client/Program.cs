using Aspire.V1;
using Grpc.Core;
using Grpc.Net.Client;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var currentResources = new List<string>();

        GrpcChannel channel = GrpcChannel.ForAddress("https://localhost:7143");
        var client = new DashboardService.DashboardServiceClient(channel);

        var cts = new CancellationTokenSource();

        var task = WatchResourcesTask(currentResources, client, cts);

        Console.ReadKey();

        cts.Cancel();
        await task;
    }

    private static async Task WatchResourcesTask(List<string> currentResources, DashboardService.DashboardServiceClient client, CancellationTokenSource cts)
    {
        var errorCount = 0;
        while (!cts.IsCancellationRequested)
        {
            if (errorCount > 0)
            {
                // Some kind of exponential backoff up to a max (5 seconds?).
                await Task.Delay(500);
            }

            try
            {
                Console.WriteLine("Starting watch");
                var call = client.WatchResources(new WatchResourcesRequest(), cancellationToken: cts.Token);

                await foreach (var response in call.ResponseStream.ReadAllAsync())
                {
                    Console.WriteLine($"Response type: {response.PayloadCase}");

                    // The most reliable way to check that a streaming call succeeded is to successfully read a response.
                    if (errorCount > 0)
                    {
                        currentResources.Clear();
                        errorCount = 0;
                    }

                    switch (response.PayloadCase)
                    {
                        case WatchResourcesResponse.PayloadOneofCase.InitialSnapshot:
                            currentResources.AddRange(response.InitialSnapshot.Items.Select(i => i.Name));
                            break;
                        case WatchResourcesResponse.PayloadOneofCase.Change:
                            currentResources.Add(response.Change.Resource.Name);
                            break;
                    }

                    Console.WriteLine($"Current resource count: {currentResources.Count}");
                }
            }
            catch (RpcException ex)
            {
                errorCount++;
                Console.WriteLine($"Error {errorCount}. Watch error: {ex.Message}");
            }
        }

        Console.WriteLine("Stopping resource watch");
    }
}