using Microsoft.AspNetCore.Mvc;
using Sample.WebApi.Models;
using Sample.WebApi.Repositories;

namespace Sample.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ShippersController : ControllerBase
{
    private readonly IShipperRepository _shipperRepository;

    public ShippersController(IShipperRepository shipperRepository)
    {
        this._shipperRepository = shipperRepository;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Shipper>>> GetAll()
    {
        var shippers = await this._shipperRepository.GetAllAsync();
        return this.Ok(shippers);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Shipper>> GetById(int id)
    {
        var shipper = await this._shipperRepository.GetByIdAsync(id);
        return shipper is null ? this.NotFound() : this.Ok(shipper);
    }

    [HttpPost]
    public async Task<ActionResult<Shipper>> Create(Shipper shipper)
    {
        // 由資料庫唯一限制判定重複，避免先查再寫時的競爭條件。
        var newId = await this._shipperRepository.CreateAsync(shipper);
        shipper.ShipperId = newId;
        return this.CreatedAtAction(nameof(this.GetById), new { id = newId }, shipper);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, Shipper shipper)
    {
        shipper.ShipperId = id;
        var updated = await this._shipperRepository.UpdateAsync(shipper);
        return updated ? this.NoContent() : this.NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await this._shipperRepository.DeleteAsync(id);
        return deleted ? this.NoContent() : this.NotFound();
    }
}
