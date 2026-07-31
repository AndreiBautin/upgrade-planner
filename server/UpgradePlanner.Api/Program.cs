using Microsoft.EntityFrameworkCore;
using UpgradePlanner.Api.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

const string DevClientCorsPolicy = "DevClient";
builder.Services.AddCors(options =>
{
    options.AddPolicy(DevClientCorsPolicy, policy =>
    {
        policy.WithOrigins("http://localhost:5176")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors(DevClientCorsPolicy);

app.UseAuthorization();

app.MapControllers();

app.Run();
