
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using TripWiseAPI.Models;
using TripWiseAPI.Services;
using TripWiseAPI.Utils;

namespace TripWiseAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            // ✅ Thêm cấu hình đọc secrets và env
            builder.Configuration
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .AddUserSecrets<Program>();
            // Add services to the container.

            builder.Services.AddDbContext<TripWiseDBContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("DBContext")));

            builder.Services.AddControllers();
            builder.Services.AddHttpClient("Gemini", client =>
            {
                client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
            });
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll",
                    builder => builder
                        .SetIsOriginAllowed(origin => true)  // Cho phép tất cả origin
                        .AllowCredentials()                  // Cho phép credentials (cookies, headers, etc.)
                        .AllowAnyMethod()                    // Cho phép mọi HTTP method (GET, POST, PUT, DELETE)
                        .AllowAnyHeader());                  // Cho phép mọi HTTP header
            });

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
                .AddJwtBearer(options =>
                {
                    options.RequireHttpsMetadata = false;
                    options.SaveToken = true;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidAudience = builder.Configuration["Jwt:Audience"],
                        ValidIssuer = builder.Configuration["Jwt:Issuer"],
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])),
                        ClockSkew = TimeSpan.Zero
                    };

                })
                .AddGoogle("Google", options =>
                    {
                    options.ClientId = builder.Configuration["Google:ClientId"];
                    options.ClientSecret = builder.Configuration["Google:ClientSecret"];
                });


            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddScoped<IAiItineraryService, AiItineraryService>();
            builder.Services.AddScoped<IPromptBuilder, PromptBuilder>();
            builder.Services.AddScoped<IJsonRepairService, JsonRepairService>();
            builder.Services.AddScoped<VectorSearchService>();
            builder.Services.AddHttpClient<IWikimediaImageService, WikimediaImageService>();
            builder.Services.AddHttpClient<WeatherService>();



            builder.Services.AddScoped<IAIGeneratePlanService, AIGeneratePlanService>();
            builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
            builder.Services.AddScoped<IPlanService, PlanService>();
            builder.Services.AddScoped<IReviewService, ReviewService>();
			builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "TripWise API", Version = "v1" });

                // ⚠️ Cấu hình cho JWT Bearer
                c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Description = "Nhập JWT vào đây với định dạng: Bearer {token}",
                    Name = "Authorization",
                    In = Microsoft.OpenApi.Models.ParameterLocation.Header,
                    Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });

                c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement()
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "oauth2",
                Name = "Bearer",
                In = Microsoft.OpenApi.Models.ParameterLocation.Header
            },
            new List<string>()
        }
    });
            });



            var app = builder.Build();
            // ✅ Tạo tài khoản admin 
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<TripWiseDBContext>();
                var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

                string adminEmail = config["Admin:Email"];
                string adminPassword = config["Admin:Password"];

                if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
                {
                    var admin = db.Users.FirstOrDefault(u => u.Email == adminEmail);

                    if (admin == null)
                    {
                        var adminUser = new User
                        {
                            UserName = "admin",
                            Email = adminEmail,
                            PasswordHash = PasswordHelper.HashPasswordBCrypt(adminPassword),
                            CreatedDate = DateTime.UtcNow,
                            Role = "ADMIN",
                            IsActive = true
                        };

                        db.Users.Add(adminUser);
                        db.SaveChanges();
                        Console.WriteLine("✅ Admin user has been created.");
                    }
                    else
                    {
                        bool passwordMatches = BCrypt.Net.BCrypt.Verify(adminPassword, admin.PasswordHash);
                        if (!passwordMatches)
                        {
                            admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword);
                            db.SaveChanges();
                            Console.WriteLine("✅ Admin password updated.");
                        }
                        
                    }
                }

            }
            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseCors("AllowAll");

            app.UseHttpsRedirection();

            app.UseAuthentication();

            app.UseAuthorization();

            app.MapControllers();
           

            app.Run();

        }
    }
}
