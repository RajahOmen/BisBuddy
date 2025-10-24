using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace BisBuddy.Services
{
    public class QueueService(ITypedLogger<QueueService> logger) : IQueueService
    {
        private readonly ITypedLogger<QueueService> logger = logger;

        private readonly CancellationTokenSource tokenSource = new();
        private readonly Channel<(string TaskName, Action Task)> taskChannel =
            Channel.CreateBounded<(string TaskName, Action Task)>(new BoundedChannelOptions(20) {
                FullMode = BoundedChannelFullMode.DropWrite,
            });
        private Task? workerLoop;

        private static async Task doWorkerLoop(
            ChannelReader<(string TaskName, Action Task)> reader,
            ITypedLogger<QueueService> logger,
            CancellationToken token
            )
        {
            try
            {
                while (await reader.WaitToReadAsync(token))
                {
                    var (taskName, task) = await reader.ReadAsync(token);
                    logger.Verbose($"[{taskName}] Executing task");
                    task();
                    logger.Verbose($"[{taskName}] Task complete");
                }
            }
            catch (OperationCanceledException)
            {
                logger.Warning($"Worker loop cancelled");
            }
        }

        public bool Enqueue(string taskName, Action task)
        {
            var count = taskChannel.Reader.CanCount
                ? $"{taskChannel.Reader.Count}"
                : "?";

            logger.Verbose($"[{taskName}] Enqueuing queue task ({count})");

            if (!taskChannel.Writer.TryWrite((taskName, task)))
            {
                logger.Warning($"[{taskName}] Failed to enqueue task");
                return false;
            }

            return true;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            cancellationToken.Register(tokenSource.Cancel);
            workerLoop = Task.Run(
                () => doWorkerLoop(taskChannel.Reader, logger, tokenSource.Token),
                cancellationToken
                );

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (!taskChannel.Writer.TryComplete())
            {
                logger.Warning($"Failed to complete task channel writer, force stopping");
                tokenSource.Cancel();
                return;
            }

            // set up a force stop in case the worker loop takes too long
            var forceStopCancelToken = new CancellationTokenSource();
            _ = Task.Delay(1000, forceStopCancelToken.Token).ContinueWith(_ =>
            {
                logger.Warning($"Cancelling queue worker loop");
                tokenSource.Cancel();
            }, forceStopCancelToken.Token);

            // try waiting for the loop to complete all tasks
            if (workerLoop != null)
            {
                logger.Verbose($"Awaiting worker loop completion");
                await workerLoop;
            }

            // worker loop exited gracefully, don't force stop
            forceStopCancelToken.Cancel();

            logger.Verbose($"Queue service complete");
        }
    }

    public interface IQueueService : IHostedService
    {
        public bool Enqueue(string taskName, Action task);
    }
}
