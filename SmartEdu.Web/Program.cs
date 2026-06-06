using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using SmartEdu.Business.Interfaces;
using SmartEdu.Business.Services;
using SmartEdu.Data;
using SmartEdu.Data.Repositories;
using SmartEdu.Web.Models;

namespace SmartEdu.Web
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllersWithViews();
            builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

            builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
            builder.Services.AddScoped<IDocumentService, DocumentService>();
            builder.Services.AddScoped<IChatService, ChatService>();
            // HttpClient factory for calling OpenAI
            builder.Services.AddHttpClient();
            builder.Services.AddScoped<ISubjectService, SubjectService>();
            // Đăng ký cấu hình từ Configuration (tự động lấy từ appsettings + secrets)
            builder.Services.Configure<GeminiSettings>(
                builder.Configuration.GetSection("Gemini"));

            builder.Services.Configure<HuggingFaceSettings>(
                builder.Configuration.GetSection("HuggingFace"));
            builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        // Chuyển toàn bộ JSON sang camelCase
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
            builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Tự động chuyển Enum sang dạng chữ trong JSON
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
            builder.Services.AddHttpClient("HuggingFace", client => {
                // Use the API inference base URL. We'll build the final models/... URI in the service to avoid
                // accidental malformed URLs when combining BaseAddress and relative paths.
                client.BaseAddress = new Uri("https://api-inference.huggingface.co/");
                var token = builder.Configuration["HuggingFace:Token"];
                if (!string.IsNullOrEmpty(token))
                {
                    // Set Authorization header using AuthenticationHeaderValue to ensure correct formatting
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }
            });
            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
    });
            builder.Services.AddScoped<IPermissionService, PermissionService>();
            builder.Services.AddScoped<IAccountService, AccountService>();
            builder.Services.AddScoped<IEmailService, EmailService>();
            builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
            // Register filter for requiring password change after first login
            builder.Services.AddScoped<SmartEdu.Web.Filters.RequirePasswordChangeFilter>();
            builder.Services.AddControllersWithViews(options =>
            {
                options.Filters.AddService<SmartEdu.Web.Filters.RequirePasswordChangeFilter>();
            });
            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var context = services.GetRequiredService<AppDbContext>();

                // Đảm bảo database đã được tạo (áp dụng migration)
                context.Database.Migrate();

                // Chạy seeder
                DataSeeder.Seed(context);
            }

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }
    }
}
