using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PublicUi.Shared;
using static HeartbeatService.Heartbeat;

namespace PublicUi
{
    public class HeartbeatStreamService : IHostedService
    {
        private GrpcChannel _channel;
        private HeartbeatClient _heartbeatClient;
        private readonly ILogger<HeartbeatStreamService> _logger;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly IHubContext<HeartbeatHub> _heartbeatHub;

        public HeartbeatStreamService(ILogger<HeartbeatStreamService> logger, 
            IHubContext<HeartbeatHub> heartbeatHub)
        {
            _heartbeatHub = heartbeatHub;
            _logger = logger;
            _cancellationTokenSource = new CancellationTokenSource();
        }

#pragma warning disable CS4014
        public Task StartAsync(CancellationToken cancellationToken)
        {
            // -----------------
            // you'd want to remove this line for production use, once your certs are prod
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            // -----------------
            
            _channel = GrpcChannel.ForAddress(Strings.GrpcServiceUrl);
            _heartbeatClient = new HeartbeatClient(_channel);

            Task.Run(async () => {
                await foreach (var response in _heartbeatClient.StreamHeartbeats(new Empty(), 
                    cancellationToken: _cancellationTokenSource.Token).ResponseStream.ReadAllAsync())
                {
                    await _heartbeatHub.Clients.All.SendAsync(Strings.HeartbeatReceivedEventName, response.HostName, response.HostTimeStamp.ToDateTime());
                    _logger.LogInformation($"Streamed heartbeat from {response.HostName}");
                }
            });

            return Task.CompletedTask;
        }
#pragma warning restore CS4014

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _heartbeatClient.StreamHeartbeats(new Empty(), 
                cancellationToken: new CancellationToken(true));
                
            _channel?.Dispose();
            return Task.CompletedTask;
        }
    }
}