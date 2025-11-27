using Custom.Framework.Azure.Cosmos.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Custom.Framework.Azure.Cosmos;

/// <summary>
/// Repository implementation for Order operations using EF Core Cosmos provider
/// </summary>
public class OrderRepository : IOrderRepository
{
    private readonly OrderDbContext _context;
    private readonly CosmosDbOptions _options;
    private readonly ILogger<OrderRepository> _logger;

    public OrderRepository(
        OrderDbContext context,
        CosmosDbOptions options,
        ILogger<OrderRepository> logger)
    {
        _context = context;
        _options = options;
        _logger = logger;
    }

    public async Task<OrderContext> CreateOrderAsync(OrderContext order, CancellationToken cancellationToken = default)
    {
        try
        {
            // Set timestamps
            order.CreatedAt = DateTime.UtcNow;
            order.UpdatedAt = DateTime.UtcNow;

            // Set TTL based on status
            SetTtlByStatus(order);

            // Calculate expiration time
            if (order.Ttl.HasValue && order.Ttl.Value > 0)
            {
                order.ExpiresAt = DateTime.UtcNow.AddSeconds(order.Ttl.Value);
            }

            _context.Orders.Add(order);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Order created: {OrderId}, Hotel: {HotelCode}, Status: {Status}, TTL: {Ttl}s",
                order.Id, order.HotelCode, order.Status, order.Ttl);

            return order;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating order {OrderId} for hotel {HotelCode}", order.Id, order.HotelCode);
            throw;
        }
    }

    public async Task<OrderContext?> GetOrderByIdAsync(string id, string hotelCode, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.Orders
                .WithPartitionKey(hotelCode)
                .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting order {OrderId} for hotel {HotelCode}", id, hotelCode);
            throw;
        }
    }

    public async Task<OrderContext?> GetOrderBySessionIdAsync(string sessionId, string hotelCode, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.Orders
                .WithPartitionKey(hotelCode)
                .Where(o => o.SessionId == sessionId)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting order by session {SessionId} for hotel {HotelCode}", sessionId, hotelCode);
            throw;
        }
    }

    public async Task<OrderContext> UpdateOrderAsync(OrderContext order, CancellationToken cancellationToken = default)
    {
        try
        {
            order.UpdatedAt = DateTime.UtcNow;

            // Update TTL based on status change
            SetTtlByStatus(order);

            // Recalculate expiration time
            if (order.Ttl.HasValue && order.Ttl.Value > 0)
            {
                order.ExpiresAt = DateTime.UtcNow.AddSeconds(order.Ttl.Value);
            }

            _context.Orders.Update(order);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Order updated: {OrderId}, Hotel: {HotelCode}, Status: {Status}",
                order.Id, order.HotelCode, order.Status);

            return order;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict updating order {OrderId}", order.Id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating order {OrderId} for hotel {HotelCode}", order.Id, order.HotelCode);
            throw;
        }
    }

    public async Task<OrderContext> UpdateOrderStatusAsync(string id, string hotelCode, OrderStatus status, CancellationToken cancellationToken = default)
    {
        var order = await GetOrderByIdAsync(id, hotelCode, cancellationToken);
        if (order == null)
        {
            throw new InvalidOperationException($"Order {id} not found in hotel {hotelCode}");
        }

        order.Status = status;
        return await UpdateOrderAsync(order, cancellationToken);
    }

    public async Task<OrderContext> UpdateOrderStepAsync(string id, string hotelCode, string step, CancellationToken cancellationToken = default)
    {
        var order = await GetOrderByIdAsync(id, hotelCode, cancellationToken);
        if (order == null)
        {
            throw new InvalidOperationException($"Order {id} not found in hotel {hotelCode}");
        }

        order.CurrentStep = step;
        return await UpdateOrderAsync(order, cancellationToken);
    }

    public async Task<bool> DeleteOrderAsync(string id, string hotelCode, CancellationToken cancellationToken = default)
    {
        try
        {
            var order = await GetOrderByIdAsync(id, hotelCode, cancellationToken);
            if (order == null)
            {
                return false;
            }

            _context.Orders.Remove(order);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Order deleted: {OrderId}, Hotel: {HotelCode}", id, hotelCode);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting order {OrderId} for hotel {HotelCode}", id, hotelCode);
            throw;
        }
    }

    public async Task<List<OrderContext>> GetOrdersByHotelAsync(string hotelCode, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.Orders
                .WithPartitionKey(hotelCode)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting orders for hotel {HotelCode}", hotelCode);
            throw;
        }
    }

    public async Task<List<OrderContext>> GetOrdersByStatusAsync(string hotelCode, OrderStatus status, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.Orders
                .WithPartitionKey(hotelCode)
                .Where(o => o.Status == status)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting orders with status {Status} for hotel {HotelCode}", status, hotelCode);
            throw;
        }
    }

    public async Task<List<OrderContext>> GetPendingOrdersAsync(string hotelCode, CancellationToken cancellationToken = default)
    {
        return await GetOrdersByStatusAsync(hotelCode, OrderStatus.Pending, cancellationToken);
    }

    public async Task<List<OrderContext>> GetExpiringOrdersAsync(string hotelCode, int withinMinutes = 5, CancellationToken cancellationToken = default)
    {
        try
        {
            var threshold = DateTime.UtcNow.AddMinutes(withinMinutes);
            
            return await _context.Orders
                .WithPartitionKey(hotelCode)
                .Where(o => o.ExpiresAt.HasValue && o.ExpiresAt.Value <= threshold)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expiring orders for hotel {HotelCode}", hotelCode);
            throw;
        }
    }

    public async Task<bool> OrderExistsAsync(string id, string hotelCode, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.Orders
                .WithPartitionKey(hotelCode)
                .AnyAsync(o => o.Id == id, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if order {OrderId} exists for hotel {HotelCode}", id, hotelCode);
            throw;
        }
    }

    /// <summary>
    /// Set TTL based on order status
    /// </summary>
    private void SetTtlByStatus(OrderContext order)
    {
        order.Ttl = order.Status switch
        {
            OrderStatus.Succeeded => _options.SucceededTtlSeconds, // Long TTL for successful orders
            OrderStatus.Failed => _options.DefaultTtlSeconds,      // Short TTL for failed orders
            OrderStatus.Cancelled => _options.DefaultTtlSeconds,   // Short TTL for cancelled orders
            OrderStatus.Expired => _options.DefaultTtlSeconds,     // Short TTL for expired orders
            _ => _options.DefaultTtlSeconds                        // Default TTL for pending/in-progress
        };
    }
}
