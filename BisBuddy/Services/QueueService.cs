using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace BisBuddy.Services
{
    public class QueueService(ITypedLogger<QueueService> logger) : IQueueService
    {
        private readonly ITypedLogger<QueueService> logger = logger;

        private readonly CancellationTokenSource tokenSource = new();
        private readonly ConcurrentQueue<Action> taskQueue = new();
        private readonly SemaphoreSlim signal = new(0);
        private volatile bool queueOpen = true;
        private Task? workerLoop;

        private async Task doWorkerLoop()
        {
            while (true)
            {
                await signal.WaitAsync(tokenSource.Token); // Wait until a task is available

                if (!queueOpen && taskQueue.IsEmpty)
                    break;

                if (taskQueue.TryDequeue(out var task))
                {
                    logger.Verbose($"Executing queue task");
                    task(); // Execute the task
                    logger.Verbose($"Queue task complete");
                }
            }
        }

        public bool Enqueue(Action task)
        {
            if (!queueOpen)
                return false;

            logger.Verbose($"Enqueuing queue task");

            taskQueue.Enqueue(task);
            signal.Release();

            return true;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            cancellationToken.Register(tokenSource.Cancel);
            queueOpen = true;
            workerLoop = Task.Run(doWorkerLoop, CancellationToken.None);

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            cancellationToken.Register(tokenSource.Cancel);

            logger.Verbose($"Awaiting queue stop");
            queueOpen = false;
            signal.Release();
            if (workerLoop is not null)
                await workerLoop;

            logger.Verbose($"Queue stopped");
        }
    }

    public interface IQueueService : IHostedService
    {
        public bool Enqueue(Action task);
    }
}
