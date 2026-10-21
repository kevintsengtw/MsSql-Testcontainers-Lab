namespace Sample.WebApi.Models;

public class Order
{
    public int OrderId { get; set; }

    public int ShipperId { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public decimal Freight { get; set; }

    public DateTime OrderDate { get; set; }
}
