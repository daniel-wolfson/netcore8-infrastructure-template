namespace Custom.Framework
{
    /// <summary>
    /// Represents the current state of a Kafka transaction during message processing.
    /// </summary>
    public enum TransactionState
    {
        /// <summary>
        /// The transaction has not been initialized yet.
        /// </summary>
        NotInitialized,
        
        /// <summary>
        /// The transaction is initialized and ready to begin processing.
        /// </summary>
        Ready,
        
        /// <summary>
        /// Currently processing messages within an active transaction.
        /// </summary>
        InTransaction,
        
        /// <summary>
        /// An error occurred during transaction processing that requires handling.
        /// </summary>
        Error
    }
}