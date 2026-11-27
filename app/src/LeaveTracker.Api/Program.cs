using System.Text.Json;
using System.Text.Json.Serialization;
using LeaveTracker.Api.Data;
using LeaveTracker.Api.Endpoints;
using LeaveTracker.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Leave") ?? "Data Source=leave.db";
builder.Services.AddDbContext<LeaveDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<EmployeeService>();
builder.Services.AddScoped<LeaveRequestService>();
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LeaveDbContext>();
    await db.Database.EnsureCreatedAsync();
    await DemoSeeder.SeedAsync(db);
}

app.MapHealthEndpoints();
app.MapEmployeeEndpoints();
app.MapLeaveRequestEndpoints();

await app.RunAsync();

public partial class Program;
