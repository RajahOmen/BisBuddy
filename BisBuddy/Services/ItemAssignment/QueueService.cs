using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace BisBuddy.Services.ItemAssignment
{
    public class QueueService : IQueueService
    {
        private readonly CancellationTokenSource tokenSource = new();
        private readonly ConcurrentQueue<Action> taskQueue = new();
        private readonly SemaphoreSlim signal = new(0);
        private volatile bool queueOpen = false;
        private Task? workerLoop;

        private async Task doWorkerLoop()
        {
            while (true)
            {
                await signal.WaitAsync(tokenSource.Token); // Wait until a task is available

                if (!queueOpen && taskQueue.IsEmpty)
                    break;

                if (taskQueue.TryDequeue(out var task))
                    task(); // Execute the task
            }
        }

        public void Enqueue(Action task)
        {
            if (!queueOpen)
                return;

            taskQueue.Enqueue(task);
            signal.Release();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            cancellationToken.Register(tokenSource.Cancel);
            workerLoop = Task.Run(doWorkerLoop, CancellationToken.None);
            queueOpen = true;

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            cancellationToken.Register(tokenSource.Cancel);
            queueOpen = false;
            signal.Release();
            if (workerLoop is not null)
                await workerLoop;
        }
    }

    public interface IQueueService : IHostedService
    {
        public void Enqueue(Action task);
    }
}
