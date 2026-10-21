using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Sample.WebApi.Models;
using Sample.WebApi.Repositories;

namespace Sample.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderRepository _orderRepository;

    public OrdersController(IOrderRepository orderRepository)
    {
        this._orderRepository = orderRepository;
    }

    [HttpGet("by-shipper/{shipperId:int}")]
    public async Task<ActionResult<IEnumerable<Order>>> GetByShipper(
        int shipperId,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 200)] int pageSize = 20)
    {
        var orders = await this._orderRepository.GetByShipperAsync(shipperId, page, pageSize);
        return this.Ok(orders);
    }
}
