using Sample.WebApi.Models;

namespace Sample.WebApi.Repositories;

public interface IShipperRepository
{
    Task<IEnumerable<Shipper>> GetAllAsync();

    Task<Shipper?> GetByIdAsync(int shipperId);

    Task<int> CreateAsync(Shipper shipper);

    Task<bool> UpdateAsync(Shipper shipper);

    Task<bool> DeleteAsync(int shipperId);
}
