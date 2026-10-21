using System.ComponentModel.DataAnnotations;

namespace Sample.WebApi.Models;

public class Shipper
{
    public int ShipperId { get; set; }

    [Required(ErrorMessage = "CompanyName 不可為空白。")]
    [StringLength(40, ErrorMessage = "CompanyName 不可超過 40 個字元。")]
    public string CompanyName { get; set; } = string.Empty;

    [StringLength(24, ErrorMessage = "Phone 不可超過 24 個字元。")]
    public string? Phone { get; set; }
}
