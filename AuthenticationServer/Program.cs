using AuthCoreSdk.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Use AuthCoreSdk to configure services
builder.Services.AddAuthCoreSdkServer(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Use AuthCoreSdk to configure middleware and endpoints
app.UseAuthCoreSdkServer();

app.Run();