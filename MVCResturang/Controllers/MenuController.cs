using Microsoft.AspNetCore.Mvc;
using MVCResturang.Models;
using System.Text.Json;

namespace MVCResturang.Controllers
{
    public class MenuController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public MenuController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<IActionResult> Index()
        {
            var client = _httpClientFactory.CreateClient("RestaurantAPI");
            
            try
            {
                var response = await client.GetAsync("api/MenuItem");
                
                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    var menuItems = JsonSerializer.Deserialize<List<MenuItem>>(jsonString, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });
                    
                    return View(menuItems ?? new List<MenuItem>());
                }
                else
                {
                    ViewBag.Error = "Kunde inte hämta menyn från servern";
                    return View(new List<MenuItem>());
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = $"Fel vid hämtning av meny: {ex.Message}";
                return View(new List<MenuItem>());
            }
        }
    }
}