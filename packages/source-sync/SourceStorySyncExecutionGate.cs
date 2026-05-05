namespace SekaiPlatform.SourceSync;

/// <summary>
/// Tracks whether this service process is already accepting or running one source story sync.
/// </summary>
public sealed class SourceStorySyncExecutionGate
{
    private const long PendingJobReservationKey = 0;
    private const long NewJobExecutionKey = -1;
    private readonly object sync = new();
    private long? activeKey;

    /// <summary>
    /// Reserves the process-local sync slot while a manual pending job row is being created.
    /// </summary>
    /// <returns><see langword="true"/> when no sync is active in this process.</returns>
    public bool TryReservePendingJob()
    {
        lock (sync)
        {
            if (activeKey is not null)
            {
                return false;
            }

            activeKey = PendingJobReservationKey;
            return true;
        }
    }

    /// <summary>
    /// Binds a previously reserved manual sync slot to the persisted job identifier.
    /// </summary>
    /// <param name="syncJobId">Persisted sync job identifier.</param>
    public void BindReservedPendingJob(long syncJobId)
    {
        lock (sync)
        {
            if (activeKey != PendingJobReservationKey)
            {
                throw new InvalidOperationException("No pending source sync reservation exists.");
            }

            activeKey = syncJobId;
        }
    }

    /// <summary>
    /// Releases a manual sync slot that failed before it could be bound to a job identifier.
    /// </summary>
    public void ReleasePendingReservation()
    {
        Release(PendingJobReservationKey);
    }

    /// <summary>
    /// Enters the process-local sync slot for a pending manual job.
    /// </summary>
    /// <param name="syncJobId">Persisted sync job identifier.</param>
    /// <returns>A lease that releases the slot, or <see langword="null"/> when another sync is active.</returns>
    public SourceStorySyncExecutionLease? TryEnterPendingJob(long syncJobId)
    {
        lock (sync)
        {
            if (activeKey == syncJobId)
            {
                return new SourceStorySyncExecutionLease(this, syncJobId);
            }

            if (activeKey is not null)
            {
                return null;
            }

            activeKey = syncJobId;
            return new SourceStorySyncExecutionLease(this, syncJobId);
        }
    }

    /// <summary>
    /// Enters the process-local sync slot for a job that will create its own database row.
    /// </summary>
    /// <returns>A lease that releases the slot, or <see langword="null"/> when another sync is active.</returns>
    public SourceStorySyncExecutionLease? TryEnterNewJob()
    {
        lock (sync)
        {
            if (activeKey is not null)
            {
                return null;
            }

            activeKey = NewJobExecutionKey;
            return new SourceStorySyncExecutionLease(this, NewJobExecutionKey);
        }
    }

    private void Release(long key)
    {
        lock (sync)
        {
            if (activeKey == key)
            {
                activeKey = null;
            }
        }
    }

    /// <summary>
    /// Releases a process-local source sync slot when disposed.
    /// </summary>
    /// <param name="gate">Gate that owns the active slot.</param>
    /// <param name="key">Internal key identifying the active slot.</param>
    public sealed class SourceStorySyncExecutionLease(SourceStorySyncExecutionGate gate, long key) : IDisposable
    {
        private int disposed;

        /// <summary>
        /// Releases the active source sync slot once.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                gate.Release(key);
            }
        }
    }
}
