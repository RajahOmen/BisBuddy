using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace BisBuddy.ItemAssignment
{
    public class ItemAssignmentQueue
    {
        private readonly ConcurrentQueue<Action> taskQueue = new();
        private readonly SemaphoreSlim signal = new(0);
        private volatile bool running = true;

        public ItemAssignmentQueue()
        {
            Task.Run(WorkerLoop);
        }

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

        public void Stop()
        {
            running = false;
            signal.Release(); // Wake up the worker to exit gracefully
        }
    }
}
