using LeaveTracker.Api.Data;
using LeaveTracker.Api.Endpoints;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Leave") ?? "Data Source=leave.db";
builder.Services.AddDbContext<LeaveDbContext>(options => options.UseSqlite(connectionString));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LeaveDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.MapHealthEndpoints();

await app.RunAsync();

public partial class Program;
