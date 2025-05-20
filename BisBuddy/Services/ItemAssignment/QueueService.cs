using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace BisBuddy.Services.ItemAssignment
{
    public class QueueService : IQueueService
    {
        private readonly ConcurrentQueue<Action> taskQueue = new();
        private readonly SemaphoreSlim signal = new(0);
        private volatile bool running = true;

        private async Task WorkerLoop()
        {
            while (running)
            {
                await signal.WaitAsync(); // Wait until a task is available

                if (taskQueue.TryDequeue(out var task))
                {
                    task(); // Execute the task
                }
            }
        }

        public void Enqueue(Action task)
        {
            taskQueue.Enqueue(task);
            signal.Release();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Task.Run(WorkerLoop, cancellationToken);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            running = false;
            signal.Release();

            return Task.CompletedTask;
        }
    }

    public interface IQueueService : IHostedService
    {
        public void Enqueue(Action task);
    }
}
