using RedisCache;
using RedisCache.WebApi;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddRedisService("127.0.0.1", 2, 2, true, 5,3,6,300);
builder.Services.AddDbContext<BaseDbContext>();
builder.Services.AddScoped<DomainService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
