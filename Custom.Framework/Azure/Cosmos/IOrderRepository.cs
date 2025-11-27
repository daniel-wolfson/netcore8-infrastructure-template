using Custom.Framework.Azure.Cosmos.Models;

namespace Custom.Framework.Azure.Cosmos;

/// <summary>
/// Repository interface for Order operations
/// </summary>
public interface IOrderRepository
{
    /// <summary>
    /// Create a new order context
    /// </summary>
    Task<OrderContext> CreateOrderAsync(OrderContext order, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get order by ID and hotel code (partition key)
    /// </summary>
    Task<OrderContext?> GetOrderByIdAsync(string id, string hotelCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get order by session ID
    /// </summary>
    Task<OrderContext?> GetOrderBySessionIdAsync(string sessionId, string hotelCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update existing order
    /// </summary>
    Task<OrderContext> UpdateOrderAsync(OrderContext order, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update order status
    /// </summary>
    Task<OrderContext> UpdateOrderStatusAsync(string id, string hotelCode, OrderStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update order step
    /// </summary>
    Task<OrderContext> UpdateOrderStepAsync(string id, string hotelCode, string step, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete order
    /// </summary>
    Task<bool> DeleteOrderAsync(string id, string hotelCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get orders by hotel code
    /// </summary>
    Task<List<OrderContext>> GetOrdersByHotelAsync(string hotelCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get orders by status
    /// </summary>
    Task<List<OrderContext>> GetOrdersByStatusAsync(string hotelCode, OrderStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get pending orders (for cleanup or monitoring)
    /// </summary>
    Task<List<OrderContext>> GetPendingOrdersAsync(string hotelCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get expired orders (TTL about to expire)
    /// </summary>
    Task<List<OrderContext>> GetExpiringOrdersAsync(string hotelCode, int withinMinutes = 5, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if order exists
    /// </summary>
    Task<bool> OrderExistsAsync(string id, string hotelCode, CancellationToken cancellationToken = default);
}
