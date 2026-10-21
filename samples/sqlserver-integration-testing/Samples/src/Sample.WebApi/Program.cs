using Sample.WebApi.Infrastructure;
using Sample.WebApi.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<SqlConstraintExceptionHandler>();
builder.Services.AddSingleton<IDbConnectionFactory>(_ =>
    new SqlConnectionFactory(
        builder.Configuration.GetConnectionString("SampleDb")
        ?? throw new InvalidOperationException("缺少連線字串 'SampleDb'")));
builder.Services.AddScoped<IShipperRepository, ShipperRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();

var app = builder.Build();

app.UseExceptionHandler();
app.MapControllers();

app.Run();

/// <summary>
/// 讓 WebApplicationFactory 能夠參考到進入點。
/// </summary>
public partial class Program;
