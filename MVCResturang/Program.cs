var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllersWithViews();

// <-- Add distributed in-memory cache so Session fungerar korrekt
builder.Services.AddDistributedMemoryCache();

// Lägg till Session-support
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Session timeout
    options.Cookie.HttpOnly = true; // Säkerhet
    options.Cookie.IsEssential = true;
});

// Add HttpClient for API calls
builder.Services.AddHttpClient("RestaurantAPI", client =>
{
    client.BaseAddress = new Uri("https://localhost:7218/");
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Lägg till Session middleware (viktigt: före UseAuthorization)
app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
