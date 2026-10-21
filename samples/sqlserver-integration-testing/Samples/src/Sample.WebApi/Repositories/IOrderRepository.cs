using Sample.WebApi.Models;

namespace Sample.WebApi.Repositories;

public interface IOrderRepository
{
    Task<IEnumerable<Order>> GetByShipperAsync(int shipperId, int page, int pageSize);

    /// <summary>
    /// 在同一交易內建立多筆訂單，任一筆失敗則全部回復。
    /// </summary>
    Task<int> CreateBatchAsync(IEnumerable<Order> orders);
}
