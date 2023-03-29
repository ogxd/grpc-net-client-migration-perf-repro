using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Equativ.Monads.Result;

namespace GrpcMigrationRepro;

public class PessimisticTaskAwaiter<T> : INotifyCompletion
{
    private Action _continuation;
    public CancellationTokenRegistration registration;

    public void SetCompleted()
    {
        // todo: prevent this called more than once
        _continuation();
        registration.Dispose();
    } 

    public void OnCompleted(Action continuation)
    {
        _continuation = continuation;
    }

    public T result;

    public T GetResult() => result;
}

public class PessimisticTask<T>
{
    public PessimisticTask(TaskAwaiter<T> taskAwaiter, CancellationToken cancellationToken)
    {
        awaiter = new PessimisticTaskAwaiter<T>();
        lock (awaiter)
        {
            awaiter.registration = cancellationToken.Register(() =>
            {
                awaiter.SetCompleted();
            });
        }
        taskAwaiter.OnCompleted(() =>
        {
            awaiter.result = taskAwaiter.GetResult();
            awaiter.SetCompleted();
        });
    }

    public readonly PessimisticTaskAwaiter<T> awaiter;

    public PessimisticTaskAwaiter<T> GetAwaiter() => awaiter;
}

public static class TaskExtensions
{
    // public static PessimisticTask<Result<T>> WithCancellationResultX<T>(this TaskAwaiter<T> awaiter, CancellationToken cancellationToken)
    // {
    //     var tcs = new PessimisticTask<Result<T>>();
    //     awaiter.OnCompleted(_ =>
    //     {
    //         tcs.Awaiter.SetCompleted(Result<T>.Success(awaiter.GetResult()));
    //     });
    //     return tcs.Task;
    // }

    public class Registration : IDisposable
    {
        private volatile int _disposed;
        public CancellationTokenRegistration? Reg { get; set; }

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
            {
                if (Reg == null)
                {
                    Console.WriteLine("shit");
                }
                Reg.Value.Dispose();
            }
        }
    }
    
    public static Task<Result<T>> WithCancellationResult<T>(this TaskAwaiter<T> awaiter, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<Result<T>>();
        var reg = new Registration();
        
        var callback = () =>
        {
            lock (reg)
            {
                tcs.TrySetResult(Result<T>.Fail("Cancelled"));
                reg.Dispose();
            }
        };

        lock (reg)
        {
            CancellationTokenRegistration registration = cancellationToken.Register(callback);
            reg.Reg = registration;
        }
        awaiter.OnCompleted(() =>
        {
            try
            {
                T result = awaiter.GetResult();
                tcs.TrySetResult(Result<T>.Success(result));
            }
            catch (Exception ex)
            {
                tcs.TrySetResult(Result<T>.Fail(ex.ToString()));
            }
            reg.Dispose();
        });

        return tcs.Task;
    }
    
    public static Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<T>();

        bool finishedInTime = true;

        CancellationTokenRegistration registration = cancellationToken.Register(() =>
        {
            finishedInTime = false;
            tcs.TrySetCanceled(cancellationToken);
        });

        task.ContinueWith(x =>
        {
            registration.Dispose();

            // Mark the task exception as observed.
            // No matter what this has to be checked in all code paths
            if (x.Exception == null)
            {
                // Do not bother if it is already too late
                if (finishedInTime)
                {
                    // IsCanceled property is checked explicitly to avoid strange situation where `x.Result` throws TaskCancelledException
                    // which ends up as unhandled exception
                    if (x.IsCanceled)
                    {
                        tcs.TrySetCanceled();
                    }
                    else if (x.IsCompleted)
                    {
                        tcs.TrySetResult(x.Result);
                    }
                }
            }
            else if (finishedInTime)
            {
                tcs.TrySetException(x.Exception.InnerException ?? x.Exception);
            }
        }, TaskContinuationOptions.ExecuteSynchronously).ConfigureAwait(false);

        return tcs.Task;
    }

    public static Task<Result<T>> WithCancellationResult<T>(this Task<T> task, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<Result<T>>();

        bool finishedInTime = true;

        CancellationTokenRegistration registration = cancellationToken.Register(() =>
        {
            finishedInTime = false;
            tcs.TrySetResult(Result<T>.Fail("CancellationToken expired"));
        });

        task.ContinueWith(x =>
        {
            registration.Dispose();

            // Mark the task exception as observed.
            // No matter what this has to be checked in all code paths
            if (x.Exception == null)
            {
                // Do not bother if it is already too late
                if (finishedInTime)
                {
                    // IsCanceled property is checked explicitly to avoid strange situation where `x.Result` throws TaskCancelledException
                    // which ends up as unhandled exception
                    if (x.IsCanceled)
                    {
                        tcs.TrySetCanceled();
                    }
                    else if (x.IsCompleted)
                    {
                        tcs.TrySetResult(Result<T>.Success(x.Result));
                    }
                }
            }
            else if (finishedInTime)
            {
                tcs.TrySetResult(Result<T>.Fail("CancellationToken expired"));
            }
        }, TaskContinuationOptions.ExecuteSynchronously).ConfigureAwait(false);

        return tcs.Task;
    }

    public static async Task<T> WithCancellationGpt<T>(this Task<T> task, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource();
        using (cancellationToken.Register(() => tcs.TrySetResult()))
        {
            if (task != await Task.WhenAny(task, tcs.Task))
            {
                throw new OperationCanceledException(cancellationToken);
            }
        }

        return await task;
    }
    
    public static async Task<Result<T>> WithCancellationGrptResult<T>(this Task<T> task, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource();
        using (cancellationToken.Register(() => tcs.TrySetResult()))
        {
            if (task != await Task.WhenAny(task, tcs.Task))
            {
                return Result.Fail<T>("fail");
            }
        }

        return Result.Success(await task);
    }
}