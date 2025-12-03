using API___NFC.Hubs;
using API_NFC.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using API___NFC.Services;

var builder = WebApplication.CreateBuilder(args);

// ------------------------------------------------------
// SERVICES
// ------------------------------------------------------

// Razor Pages
builder.Services.AddRazorPages();

// Controllers + camelCase JSON  ✅ (IMPORTANTE)
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// SignalR
builder.Services.AddSignalR();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

// Email Sender (MailKit)
builder.Services.AddTransient<IEmailSender, EmailSender>();

// ------------------------------------------------------
// JWT CONFIG  🔐
// ------------------------------------------------------
var key = builder.Configuration["Jwt:Key"];

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
        };
    });

// ------------------------------------------------------
// BUILD APP
// ------------------------------------------------------
var app = builder.Build();

// Ambiente de desarrollo
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Middlewares
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseCors("AllowAll");

// JWT
app.UseAuthentication();
app.UseAuthorization();

// Endpoints
app.MapRazorPages();
app.MapControllers();

// SignalR Hub
app.MapHub<NfcHub>("/nfcHub");

// Redirect root → /Login
app.MapGet("/", (HttpContext ctx) =>
{
    ctx.Response.Redirect("/Login");
    return Task.CompletedTask;
});

app.Run();
