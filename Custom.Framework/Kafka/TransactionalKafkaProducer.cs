using Confluent.Kafka;

namespace Custom.Framework.Kafka
{
    /// <summary>
    /// A thread-safe wrapper around Kafka's transactional producer that manages transaction state
    /// and provides safe access to transactional operations with proper state tracking.
    /// </summary>
    /// <remarks>
    /// <para><strong>Key Features:</strong></para>
    /// <list type="bullet">
    /// <item><strong>Thread Safety:</strong> All operations are synchronized using internal locking</item>
    /// <item><strong>State Management:</strong> Tracks transaction lifecycle and prevents invalid operations</item>
    /// <item><strong>Resource Management:</strong> Implements IDisposable for proper cleanup</item>
    /// <item><strong>Error Handling:</strong> Provides safe transaction abort and error state management</item>
    /// </list>
    /// 
    /// <para><strong>Transaction Lifecycle:</strong></para>
    /// <list type="number">
    /// <item>NotInitialized → Ready (via InitializeTransactions)</item>
    /// <item>Ready → InTransaction (via BeginTransaction)</item>
    /// <item>InTransaction → Ready (via CommitTransaction or AbortTransaction)</item>
    /// <item>Any state → Error (on exceptions)</item>
    /// </list>
    /// 
    /// <para><strong>Usage Example:</strong></para>
    /// <code>
    /// using var transactionalProducer = new TransactionalKafkaProducer(producer);
    /// transactionalProducer.InitializeTransactions(TimeSpan.FromSeconds(10));
    /// 
    /// transactionalProducer.BeginTransaction();
    /// try
    /// {
    ///     var result = await transactionalProducer.ProduceAsync("topic", message, cancellationToken);
    ///     transactionalProducer.CommitTransaction(TimeSpan.FromSeconds(10));
    /// }
    /// catch
    /// {
    ///     transactionalProducer.AbortTransaction(TimeSpan.FromSeconds(5));
    ///     throw;
    /// }
    /// </code>
    /// </remarks>
    public sealed class TransactionalKafkaProducer : IDisposable
    {
        private readonly IProducer<string, byte[]> _producer;
        private readonly object _stateLock = new object();
        private readonly SemaphoreSlim _transactionLock = new SemaphoreSlim(1, 1);
        private TransactionState _state = TransactionState.NotInitialized;
        private bool _disposed = false;

        /// <summary>
        /// Gets the current transaction state in a thread-safe manner.
        /// </summary>
        /// <value>The current <see cref="TransactionState"/> of the transactional producer.</value>
        public TransactionState State
        {
            get
            {
                lock (_stateLock)
                {
                    return _state;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the producer has been initialized for transactions.
        /// </summary>
        public bool IsInitialized
        {
            get
            {
                lock (_stateLock)
                {
                    return _state != TransactionState.NotInitialized;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether there is an active transaction in progress.
        /// </summary>
        public bool IsInTransaction
        {
            get
            {
                lock (_stateLock)
                {
                    return _state == TransactionState.InTransaction;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the producer is in an error state.
        /// </summary>
        public bool IsInErrorState
        {
            get
            {
                lock (_stateLock)
                {
                    return _state == TransactionState.Error;
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TransactionalKafkaProducer"/> class.
        /// </summary>
        /// <param name="producer">The underlying Kafka producer to wrap. Must support transactions.</param>
        /// <exception cref="ArgumentNullException">Thrown when producer is null.</exception>
        public TransactionalKafkaProducer(IProducer<string, byte[]> producer)
        {
            _producer = producer ?? throw new ArgumentNullException(nameof(producer));
        }

        /// <summary>
        /// Initializes the transactional producer with the transaction coordinator.
        /// This method is thread-safe and idempotent - calling it multiple times has no effect after first successful initialization.
        /// </summary>
        /// <param name="timeout">The timeout for the initialization operation.</param>
        /// <exception cref="InvalidOperationException">Thrown when producer is in error state or already disposed.</exception>
        /// <exception cref="KafkaException">Thrown when Kafka-related errors occur during initialization.</exception>
        /// <remarks>
        /// <para>This method must be called before any transaction operations can be performed.
        /// It establishes communication with Kafka's transaction coordinator and prepares the producer
        /// for transactional operations.</para>
        /// </remarks>
        public void InitializeTransactions(TimeSpan timeout)
        {
            ThrowIfDisposed();

            lock (_stateLock)
            {
                if (_state == TransactionState.Error)
                    throw new InvalidOperationException("Cannot initialize transactions when producer is in error state");

                if (_state == TransactionState.NotInitialized)
                {
                    try
                    {
                        _producer.InitTransactions(timeout);
                        _state = TransactionState.Ready;
                    }
                    catch (InvalidOperationException ex) when (ex.Message.Contains("already initialized"))
                    {
                        // Producer was already initialized externally
                        _state = TransactionState.Ready;
                    }
                    catch (Exception)
                    {
                        _state = TransactionState.Error;
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Begins a new transaction. This method is thread-safe and will throw if a transaction is already active.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when producer is not initialized, already in transaction, or in error state.</exception>
        /// <exception cref="KafkaException">Thrown when Kafka-related errors occur while beginning transaction.</exception>
        /// <remarks>
        /// <para>After calling this method successfully, all subsequent produce operations will be part of this transaction
        /// until either CommitTransaction or AbortTransaction is called.</para>
        /// </remarks>
        public void BeginTransaction()
        {
            ThrowIfDisposed();

            lock (_stateLock)
            {
                if (_state == TransactionState.NotInitialized)
                    throw new InvalidOperationException("Producer must be initialized before beginning transaction");

                if (_state == TransactionState.Error)
                    throw new InvalidOperationException("Cannot begin transaction when producer is in error state");

                if (_state == TransactionState.InTransaction)
                    throw new InvalidOperationException("Transaction is already in progress");

                if (_state == TransactionState.Ready)
                {
                    try
                    {
                        _producer.BeginTransaction();
                        _state = TransactionState.InTransaction;
                    }
                    catch (Exception)
                    {
                        _state = TransactionState.Error;
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Commits the current transaction, making all produced messages visible to consumers.
        /// This method is thread-safe and will throw if no transaction is active.
        /// </summary>
        /// <param name="timeout">The timeout for the commit operation.</param>
        /// <exception cref="InvalidOperationException">Thrown when no transaction is active or producer is in error state.</exception>
        /// <exception cref="KafkaException">Thrown when Kafka-related errors occur during commit.</exception>
        /// <remarks>
        /// <para>Once this method completes successfully, all messages produced within this transaction
        /// become visible to consumers with ReadCommitted isolation level.</para>
        /// </remarks>
        public void CommitTransaction(TimeSpan timeout)
        {
            ThrowIfDisposed();

            lock (_stateLock)
            {
                if (_state != TransactionState.InTransaction)
                    throw new InvalidOperationException("No active transaction to commit");

                try
                {
                    _producer.CommitTransaction(timeout);
                    _state = TransactionState.Ready;
                }
                catch (Exception)
                {
                    _state = TransactionState.Error;
                    throw;
                }
            }
        }

        /// <summary>
        /// Aborts the current transaction, discarding all produced messages within the transaction.
        /// This method is thread-safe and is safe to call even if no transaction is active.
        /// </summary>
        /// <param name="timeout">The timeout for the abort operation.</param>
        /// <exception cref="KafkaException">Thrown when Kafka-related errors occur during abort.</exception>
        /// <remarks>
        /// <para>This method is designed to be safe for error handling scenarios. It will not throw
        /// if no transaction is active, making it suitable for use in finally blocks or catch handlers.</para>
        /// </remarks>
        public void AbortTransaction(TimeSpan timeout)
        {
            if (_disposed) return;

            lock (_stateLock)
            {
                if (_state == TransactionState.InTransaction)
                {
                    try
                    {
                        _producer.AbortTransaction(timeout);
                        _state = TransactionState.Ready;
                    }
                    catch (Exception ex)
                    {
                        _state = TransactionState.Error;
                        // Log the error but don't rethrow in abort scenarios
                        Console.Error.WriteLine($"Failed to abort transaction: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Produces a message asynchronously within the current transaction context.
        /// </summary>
        /// <param name="topic">The topic to produce the message to.</param>
        /// <param name="message">The message to produce.</param>
        /// <param name="cancellationToken">Cancellation token for the async operation.</param>
        /// <returns>A task representing the delivery result of the produce operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no transaction is active.</exception>
        /// <exception cref="ArgumentNullException">Thrown when topic or message is null.</exception>
        /// <remarks>
        /// <para>This method requires an active transaction. The message will only become visible to consumers
        /// after the transaction is successfully committed.</para>
        /// </remarks>
        public Task<DeliveryResult<string, byte[]>> ProduceAsync(
            string topic,
            Message<string, byte[]> message,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(topic))
                throw new ArgumentNullException(nameof(topic));

            if (message == null)
                throw new ArgumentNullException(nameof(message));

            lock (_stateLock)
            {
                if (_state != TransactionState.InTransaction)
                    throw new InvalidOperationException("Cannot produce message without an active transaction");
            }

            return _producer.ProduceAsync(topic, message, cancellationToken);
        }

        /// <summary>
        /// Safely executes a function within a transaction context, handling begin/commit/abort automatically.
        /// This method is thread-safe and supports concurrent execution.
        /// </summary>
        /// <typeparam name="TMessage">The return type of the function.</typeparam>
        /// <param name="function">The function to execute within the transaction.</param>
        /// <param name="commitTimeout">Timeout for transaction commit.</param>
        /// <param name="abortTimeout">Timeout for transaction abort on error.</param>
        /// <returns>The result of the function execution.</returns>
        /// <exception cref="ArgumentNullException">Thrown when function is null.</exception>
        /// <remarks>
        /// <para>This method provides a safe way to execute operations within a transaction.
        /// Each call creates its own transaction scope, making it safe for concurrent use.
        /// Multiple concurrent calls will be serialized to ensure transaction consistency.</para>
        /// </remarks>
        public async Task<TMessage> ExecuteInTransactionAsync<TMessage>(
            Func<TransactionalKafkaProducer, Task<TMessage>> function,
            TimeSpan? commitTimeout = null,
            TimeSpan? abortTimeout = null)
        {
            if (function == null)
                throw new ArgumentNullException(nameof(function));

            var commitTimeoutValue = commitTimeout ?? TimeSpan.FromSeconds(10);
            var abortTimeoutValue = abortTimeout ?? TimeSpan.FromSeconds(5);

            // Each transaction execution is serialized to prevent conflicts
            await _transactionLock.WaitAsync();
            try
            {
                // Initialize if needed (within lock)
                lock (_stateLock)
                {
                    if (_state == TransactionState.NotInitialized)
                    {
                        InitializeTransactions(TimeSpan.FromSeconds(10));
                    }
                }

                BeginTransaction();
                try
                {
                    var result = await function(this).ConfigureAwait(false);
                    CommitTransaction(commitTimeoutValue);
                    return result;
                }
                catch
                {
                    AbortTransaction(abortTimeoutValue);
                    throw;
                }
            }
            finally
            {
                _transactionLock.Release();
            }
        }

        /// <summary>
        /// Safely executes an action within a transaction context, handling begin/commit/abort automatically.
        /// </summary>
        /// <param name="action">The action to execute within the transaction.</param>
        /// <param name="commitTimeout">Timeout for transaction commit.</param>
        /// <param name="abortTimeout">Timeout for transaction abort on error.</param>
        /// <exception cref="ArgumentNullException">Thrown when action is null.</exception>
        public async Task ExecuteInTransactionAsync(
            Func<TransactionalKafkaProducer, Task> action,
            TimeSpan? commitTimeout = null,
            TimeSpan? abortTimeout = null)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            await ExecuteInTransactionAsync(async producer =>
            {
                await action(producer).ConfigureAwait(false);
                return 0; // Dummy return value
            }, commitTimeout, abortTimeout).ConfigureAwait(false);
        }

        /// <summary>
        /// Resets the producer from error state back to ready state.
        /// This should only be used after resolving the underlying error condition.
        /// </summary>
        /// <remarks>
        /// <para><strong>Warning:</strong> This method should be used with caution.
        /// Only call this after you've confirmed that the underlying error condition has been resolved
        /// and the Kafka producer is in a valid state.</para>
        /// </remarks>
        public void ResetFromErrorState()
        {
            ThrowIfDisposed();

            lock (_stateLock)
            {
                if (_state == TransactionState.Error)
                {
                    _state = TransactionState.Ready;
                }
            }
        }

        /// <summary>
        /// Disposes the transactional producer wrapper and the underlying producer.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            lock (_stateLock)
            {
                if (_state == TransactionState.InTransaction)
                {
                    try
                    {
                        AbortTransaction(TimeSpan.FromSeconds(5));
                    }
                    catch
                    {
                        // Ignore errors during dispose
                    }
                }

                _producer?.Dispose();
                _transactionLock?.Dispose();
                _disposed = true;
            }
        }

        /// <summary>
        /// Throws ObjectDisposedException if the producer has been disposed.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TransactionalKafkaProducer));
        }
    }
}