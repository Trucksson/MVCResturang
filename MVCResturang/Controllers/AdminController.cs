using Microsoft.AspNetCore.Mvc;
using MVCResturang.Models;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;

namespace MVCResturang.Controllers
{
    public class AdminController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public AdminController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        // Helper: Kontrollera om admin är inloggad
        private bool IsAdminLoggedIn()
        {
            return !string.IsNullOrEmpty(HttpContext.Session.GetString("AdminUsername"));
        }

        // Helper: Lägg till JWT token i requests
        private void AddAuthorizationHeader(HttpClient client)
        {
            var token = HttpContext.Session.GetString("JwtToken");
            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Authorization = 
                    new AuthenticationHeaderValue("Bearer", token);
            }
        }

        // GET: Admin/Login
        public IActionResult Login()
        {
            if (IsAdminLoggedIn())
            {
                return RedirectToAction("Dashboard");
            }

            return View(new LoginViewModel());
        }

        // POST: Admin/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var client = _httpClientFactory.CreateClient("RestaurantAPI");

                // Request body som matchar API:ts förväntningar
                var loginRequest = new 
                { 
                    username = model.Username, 
                    password = model.Password 
                };

                var jsonContent = JsonSerializer.Serialize(loginRequest);
                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                Console.WriteLine($"Login attempt: {model.Username}");
                Console.WriteLine($"Calling: api/Auth/login");

                // ✅ RÄTT ENDPOINT: api/Auth/login
                var response = await client.PostAsync("api/Auth/login", httpContent);

                Console.WriteLine($"Response status: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Response content: {responseContent}");
                    
                    using JsonDocument doc = JsonDocument.Parse(responseContent);
                    JsonElement root = doc.RootElement;

                    // Spara username i Session
                    HttpContext.Session.SetString("AdminUsername", model.Username);
                    
                    // Spara JWT token från response
                    if (root.TryGetProperty("token", out JsonElement tokenElement))
                    {
                        string token = tokenElement.GetString() ?? "";
                        HttpContext.Session.SetString("JwtToken", token);
                        Console.WriteLine($"Token saved: {token.Substring(0, Math.Min(20, token.Length))}...");
                    }

                    // Spara admin ID om det finns
                    if (root.TryGetProperty("id", out JsonElement idElement))
                    {
                        HttpContext.Session.SetInt32("AdminId", idElement.GetInt32());
                    }

                    TempData["SuccessMessage"] = $"Välkommen {model.Username}!";
                    return RedirectToAction("Dashboard");
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    ModelState.AddModelError("", "Felaktigt användarnamn eller lösenord");
                    return View(model);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Login failed: {response.StatusCode} - {errorContent}");
                    ModelState.AddModelError("", $"Login misslyckades. Status: {response.StatusCode}");
                    return View(model);
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Network Error: {ex.Message}");
                ModelState.AddModelError("", "Kunde inte ansluta till servern. Kontrollera att API:t körs på https://localhost:7218/");
                return View(model);
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"JSON Error: {ex.Message}");
                ModelState.AddModelError("", "Fel vid läsning av svar från servern.");
                return View(model);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected Error: {ex.Message}");
                ModelState.AddModelError("", "Ett oväntat fel uppstod.");
                return View(model);
            }
        }

        // GET: Admin/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            if (!IsAdminLoggedIn())
            {
                TempData["ErrorMessage"] = "Du måste logga in för att komma åt admin-sidan.";
                return RedirectToAction("Login");
            }

            try
            {
                var client = _httpClientFactory.CreateClient("RestaurantAPI");
                AddAuthorizationHeader(client);

                ViewBag.AdminUsername = HttpContext.Session.GetString("AdminUsername");

                // Hämta bokningsstatistik
                try
                {
                    var bookingsResponse = await client.GetAsync("api/Booking");
                    if (bookingsResponse.IsSuccessStatusCode)
                    {
                        var bookingsJson = await bookingsResponse.Content.ReadAsStringAsync();
                        using JsonDocument doc = JsonDocument.Parse(bookingsJson);
                        JsonElement root = doc.RootElement;

                        int totalBookings = 0;
                        int todayBookings = 0;

                        if (root.ValueKind == JsonValueKind.Array)
                        {
                            totalBookings = root.GetArrayLength();

                            foreach (JsonElement booking in root.EnumerateArray())
                            {
                                if (booking.TryGetProperty("bookingTime", out JsonElement bookingTimeElement))
                                {
                                    DateTime bookingTime = bookingTimeElement.GetDateTime();
                                    if (bookingTime.Date == DateTime.Today)
                                    {
                                        todayBookings++;
                                    }
                                }
                            }
                        }

                        ViewBag.TotalBookings = totalBookings;
                        ViewBag.TodayBookings = todayBookings;
                    }
                    else
                    {
                        Console.WriteLine($"Bookings stats fetch failed: {bookingsResponse.StatusCode}");
                        ViewBag.TotalBookings = 0;
                        ViewBag.TodayBookings = 0;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Bookings Stats Error: {ex.Message}");
                    ViewBag.TotalBookings = 0;
                    ViewBag.TodayBookings = 0;
                }

                // Hämta meny-statistik — använd singular endpoint som API:et exponerar
                try
                {
                    var menuResponse = await client.GetAsync("api/MenuItem"); // <- ändring: singular
                    if (menuResponse.IsSuccessStatusCode)
                    {
                        var menuJson = await menuResponse.Content.ReadAsStringAsync();
                        var menuItems = JsonSerializer.Deserialize<List<MenuItem>>(menuJson,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        ViewBag.TotalMenuItems = menuItems?.Count ?? 0;
                    }
                    else
                    {
                        var menuErr = await menuResponse.Content.ReadAsStringAsync();
                        Console.WriteLine($"MenuItems Stats fetch failed: {menuResponse.StatusCode} - {menuErr}");
                        ViewBag.TotalMenuItems = 0;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"MenuItems Stats Error: {ex.Message}");
                    ViewBag.TotalMenuItems = 0;
                }

                return View();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Dashboard Error: {ex.Message}");
                ViewBag.ErrorMessage = "Kunde inte hämta statistik";
                ViewBag.AdminUsername = HttpContext.Session.GetString("AdminUsername");
                ViewBag.TotalBookings = 0;
                ViewBag.TodayBookings = 0;
                ViewBag.TotalMenuItems = 0;
                return View();
            }
        }

        // GET: Admin/Logout
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            TempData["SuccessMessage"] = "Du har loggats ut.";
            return RedirectToAction("Index", "Home");
        }

        // Placeholder actions

        // GET: Admin/MenuItems - Visa alla menyobjekt
        public async Task<IActionResult> MenuItems()
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction("Login");
            }

            try
            {
                var client = _httpClientFactory.CreateClient("RestaurantAPI");
                AddAuthorizationHeader(client);

                Console.WriteLine("Calling: api/MenuItem");
                
                // ✅ Använd samma endpoint som MenuController
                var response = await client.GetAsync("api/MenuItem");

                Console.WriteLine($"Response: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    var menuItems = JsonSerializer.Deserialize<List<MenuItem>>(jsonString,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    return View(menuItems ?? new List<MenuItem>());
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    TempData["ErrorMessage"] = "Din session har gått ut.";
                    return RedirectToAction("Logout");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error: {errorContent}");
                    ViewBag.ErrorMessage = $"Kunde inte hämta menyobjekt. Status: {response.StatusCode}";
                    return View(new List<MenuItem>());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
                ViewBag.ErrorMessage = "Ett fel uppstod";
                return View(new List<MenuItem>());
            }
        }

        // GET: Admin/CreateMenuItem - Visa formulär för ny rätt
        public IActionResult CreateMenuItem()
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction("Login");
            }

            return View(new MenuItem());
        }

        // POST: Admin/CreateMenuItem - Skapa ny rätt
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateMenuItem(MenuItem model)
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction("Login");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var client = _httpClientFactory.CreateClient("RestaurantAPI");
                AddAuthorizationHeader(client);

                var menuItemRequest = new
                {
                    name = model.Name,
                    price = model.Price,
                    description = model.Description,
                    isPopular = model.IsPopular,
                    bildUrl = model.BildUrl
                };

                var jsonContent = JsonSerializer.Serialize(menuItemRequest);
                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                Console.WriteLine($"POST to: api/MenuItem");
                Console.WriteLine($"Body: {jsonContent}");

                // ✅ Samma endpoint som GET
                var response = await client.PostAsync("api/MenuItem", httpContent);

                Console.WriteLine($"Response: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = $"Rätten '{model.Name}' har lagts till!";
                    return RedirectToAction("MenuItems");
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    TempData["ErrorMessage"] = "Din session har gått ut.";
                    return RedirectToAction("Logout");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"POST Error: {response.StatusCode} - {errorContent}");
                    ModelState.AddModelError("", $"Kunde inte lägga till rätt. Status: {response.StatusCode}");
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
                ModelState.AddModelError("", "Ett oväntat fel uppstod.");
                return View(model);
            }
        }

        // GET: Admin/EditMenuItem - Visa formulär för redigering
        public async Task<IActionResult> EditMenuItem(int id)
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction("Login");
            }

            try
            {
                var client = _httpClientFactory.CreateClient("RestaurantAPI");
                AddAuthorizationHeader(client);

                Console.WriteLine($"Fetching menu item {id} for editing...");

                var response = await client.GetAsync($"api/MenuItem/{id}");

                Console.WriteLine($"Response: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    var menuItem = JsonSerializer.Deserialize<MenuItem>(jsonString,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (menuItem == null)
                    {
                        TempData["ErrorMessage"] = "Kunde inte hitta rätten";
                        return RedirectToAction("MenuItems");
                    }

                    return View(menuItem);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    TempData["ErrorMessage"] = "Din session har gått ut.";
                    return RedirectToAction("Logout");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error: {errorContent}");
                    TempData["ErrorMessage"] = $"Kunde inte hämta rätt. Status: {response.StatusCode}";
                    return RedirectToAction("MenuItems");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Edit Get Error: {ex.Message}");
                TempData["ErrorMessage"] = "Ett fel uppstod";
                return RedirectToAction("MenuItems");
            }
        }

        // POST: Admin/EditMenuItem - Uppdatera rätt
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditMenuItem(MenuItem model)
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction("Login");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var client = _httpClientFactory.CreateClient("RestaurantAPI");
                AddAuthorizationHeader(client);

                var menuItemRequest = new
                {
                    id = model.Id,
                    name = model.Name,
                    price = model.Price,
                    description = model.Description,
                    isPopular = model.IsPopular,
                    bildUrl = model.BildUrl
                };

                var jsonContent = JsonSerializer.Serialize(menuItemRequest);
                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                Console.WriteLine($"PUT to: api/MenuItem/{model.Id}");
                Console.WriteLine($"Body: {jsonContent}");

                var response = await client.PutAsync($"api/MenuItem/{model.Id}", httpContent);

                Console.WriteLine($"Response: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = $"Rätten '{model.Name}' har uppdaterats!";
                    return RedirectToAction("MenuItems");
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    TempData["ErrorMessage"] = "Din session har gått ut.";
                    return RedirectToAction("Logout");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"PUT Error: {response.StatusCode} - {errorContent}");
                    ModelState.AddModelError("", $"Kunde inte uppdatera rätt. Status: {response.StatusCode}");
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Edit Post Error: {ex.Message}");
                ModelState.AddModelError("", "Ett oväntat fel uppstod.");
                return View(model);
            }
        }

        // POST: Admin/DeleteMenuItem - Ta bort rätt
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMenuItem(int id)
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction("Login");
            }

            try
            {
                var client = _httpClientFactory.CreateClient("RestaurantAPI");
                AddAuthorizationHeader(client);

                Console.WriteLine($"DELETE: api/MenuItem/{id}");

                var response = await client.DeleteAsync($"api/MenuItem/{id}");

                Console.WriteLine($"Response: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Rätten har tagits bort";
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    TempData["ErrorMessage"] = "Din session har gått ut.";
                    return RedirectToAction("Logout");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"DELETE Error: {response.StatusCode} - {errorContent}");
                    TempData["ErrorMessage"] = "Kunde inte ta bort rätt";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Delete Error: {ex.Message}");
                TempData["ErrorMessage"] = "Ett fel uppstod";
            }

            return RedirectToAction("MenuItems");
        }

        // GET: Admin/Bookings - Visa alla bokningar
        public async Task<IActionResult> Bookings()
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction("Login");
            }

            try
            {
                var client = _httpClientFactory.CreateClient("RestaurantAPI");
                AddAuthorizationHeader(client);

                Console.WriteLine("Fetching bookings from API...");

                var response = await client.GetAsync("api/Booking");

                Console.WriteLine($"Bookings response: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Bookings JSON: {jsonString}");

                    // Parse JSON manuellt eftersom API:ts struktur kan skilja sig
                    using JsonDocument doc = JsonDocument.Parse(jsonString);
                    JsonElement root = doc.RootElement;

                    var bookings = new List<BookingAdminViewModel>();

                    if (root.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement booking in root.EnumerateArray())
                        {
                            var bookingViewModel = new BookingAdminViewModel
                            {
                                Id = booking.TryGetProperty("id", out var id) ? id.GetInt32() : 0,
                                BookingTime = booking.TryGetProperty("bookingTime", out var time) ? time.GetDateTime() : DateTime.MinValue,
                                NumberOfGuests = booking.TryGetProperty("numberOfGuests", out var guests) ? guests.GetInt32() : 0,
                                CustomerId = booking.TryGetProperty("customerId", out var custId) ? custId.GetInt32() : 0,
                                TableId = booking.TryGetProperty("tableId", out var tblId) ? tblId.GetInt32() : 0
                            };

                            // Hämta customer info om det finns
                            if (booking.TryGetProperty("customer", out var customer))
                            {
                                bookingViewModel.CustomerName = customer.TryGetProperty("name", out var name) ? (name.GetString() ?? "") : "";
                                bookingViewModel.CustomerEmail = customer.TryGetProperty("email", out var email) ? email.GetString() : "";
                                bookingViewModel.CustomerPhone = customer.TryGetProperty("phoneNumber", out var phone) ? phone.GetString() : "";
                            }

                            // Hämta table info om det finns
                            if (booking.TryGetProperty("table", out var table))
                            {
                                bookingViewModel.TableNumber = table.TryGetProperty("tableNumber", out var tblNum) ? tblNum.GetInt32() : 0;
                                bookingViewModel.TableCapacity = table.TryGetProperty("capacity", out var cap) ? cap.GetInt32() : 0;
                            }

                            bookings.Add(bookingViewModel);
                        }
                    }

                    // Sortera bokningar - framtida först, sedan datum
                    var sortedBookings = bookings.OrderBy(b => b.BookingTime).ToList();

                    return View(sortedBookings);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    TempData["ErrorMessage"] = "Din session har gått ut.";
                    return RedirectToAction("Logout");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error: {errorContent}");
                    ViewBag.ErrorMessage = $"Kunde inte hämta bokningar. Status: {response.StatusCode}";
                    return View(new List<BookingAdminViewModel>());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Bookings Error: {ex.Message}");
                ViewBag.ErrorMessage = "Ett fel uppstod";
                return View(new List<BookingAdminViewModel>());
            }
        }

        // GET: Admin/EditBooking - Visa formulär för redigering
        public async Task<IActionResult> EditBooking(int id)
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction("Login");
            }

            try
            {
                var client = _httpClientFactory.CreateClient("RestaurantAPI");
                AddAuthorizationHeader(client);

                Console.WriteLine($"Fetching booking {id} for editing...");

                var response = await client.GetAsync($"api/Booking/{id}");

                Console.WriteLine($"Response: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    
                    using JsonDocument doc = JsonDocument.Parse(jsonString);
                    JsonElement root = doc.RootElement;

                    var booking = new BookingEditViewModel
                    {
                        Id = root.TryGetProperty("id", out var bookingId) ? bookingId.GetInt32() : 0,
                        BookingTime = root.TryGetProperty("bookingTime", out var time) ? time.GetDateTime() : DateTime.Now,
                        NumberOfGuests = root.TryGetProperty("numberOfGuests", out var guests) ? guests.GetInt32() : 1,
                        CustomerId = root.TryGetProperty("customerId", out var custId) ? custId.GetInt32() : 0,
                        TableId = root.TryGetProperty("tableId", out var tblId) ? tblId.GetInt32() : 1
                    };

                    // Hämta customer info
                    if (root.TryGetProperty("customer", out var customer))
                    {
                        booking.CustomerName = customer.TryGetProperty("name", out var name) ? name.GetString() : "";
                    }

                    return View(booking);
                }
                else
                {
                    TempData["ErrorMessage"] = "Kunde inte hämta bokning";
                    return RedirectToAction("Bookings");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Edit Get Error: {ex.Message}");
                TempData["ErrorMessage"] = "Ett fel uppstod";
                return RedirectToAction("Bookings");
            }
        }

        // POST: Admin/EditBooking - Uppdatera bokning
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditBooking(BookingEditViewModel model)
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction("Login");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var client = _httpClientFactory.CreateClient("RestaurantAPI");
                AddAuthorizationHeader(client);

                var bookingRequest = new
                {
                    id = model.Id,
                    bookingTime = model.BookingTime,
                    numberOfGuests = model.NumberOfGuests,
                    customerId = model.CustomerId,
                    tableId = model.TableId
                };

                var jsonContent = JsonSerializer.Serialize(bookingRequest);
                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                Console.WriteLine($"PUT to: api/Booking/{model.Id}");
                Console.WriteLine($"Body: {jsonContent}");

                var response = await client.PutAsync($"api/Booking/{model.Id}", httpContent);

                Console.WriteLine($"Response: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Bokning uppdaterad!";
                    return RedirectToAction("Bookings");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error: {errorContent}");
                    ModelState.AddModelError("", $"Kunde inte uppdatera bokning. Status: {response.StatusCode}");
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Edit Post Error: {ex.Message}");
                ModelState.AddModelError("", "Ett fel uppstod");
                return View(model);
            }
        }

        // POST: Admin/DeleteBooking - Ta bort bokning
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBooking(int id)
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction("Login");
            }

            try
            {
                var client = _httpClientFactory.CreateClient("RestaurantAPI");
                AddAuthorizationHeader(client);

                Console.WriteLine($"DELETE: api/Booking/{id}");

                var response = await client.DeleteAsync($"api/Booking/{id}");

                Console.WriteLine($"Response: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Bokning har tagits bort";
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error: {errorContent}");
                    TempData["ErrorMessage"] = "Kunde inte ta bort bokning";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Delete Error: {ex.Message}");
                TempData["ErrorMessage"] = "Ett fel uppstod";
            }

            return RedirectToAction("Bookings");
        }

        // GET: Admin/Tables - Visa alla bord
        public async Task<IActionResult> Tables()
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction("Login");
            }

            try
            {
                var client = _httpClientFactory.CreateClient("RestaurantAPI");
                AddAuthorizationHeader(client);

                Console.WriteLine("Fetching tables from API...");

                var response = await client.GetAsync("api/Table");

                Console.WriteLine($"Tables response: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    var tables = JsonSerializer.Deserialize<List<Table>>(jsonString,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    return View(tables ?? new List<Table>());
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    TempData["ErrorMessage"] = "Din session har gått ut.";
                    return RedirectToAction("Logout");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error: {errorContent}");
                    ViewBag.ErrorMessage = $"Kunde inte hämta bord. Status: {response.StatusCode}";
                    return View(new List<Table>());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Tables Error: {ex.Message}");
                ViewBag.ErrorMessage = "Ett fel uppstod";
                return View(new List<Table>());
            }
        }

        // GET: Admin/CreateTable - Visa formulär för nytt bord
        public IActionResult CreateTable()
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction("Login");
            }

            return View(new Table());
        }

        // POST: Admin/CreateTable - Skapa nytt bord
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTable(Table model)
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction("Login");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var client = _httpClientFactory.CreateClient("RestaurantAPI");
                AddAuthorizationHeader(client);

                var tableRequest = new
                {
                    tableNumber = model.TableNumber,
                    capacity = model.Capacity
                };

                var jsonContent = JsonSerializer.Serialize(tableRequest);
                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                Console.WriteLine($"POST to: api/Table");
                Console.WriteLine($"Body: {jsonContent}");

                var response = await client.PostAsync("api/Table", httpContent);

                Console.WriteLine($"Response: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = $"Bord {model.TableNumber} har lagts till!";
                    return RedirectToAction("Tables");
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    TempData["ErrorMessage"] = "Din session har gått ut.";
                    return RedirectToAction("Logout");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"POST Error: {response.StatusCode} - {errorContent}");
                    ModelState.AddModelError("", $"Kunde inte lägga till bord. Status: {response.StatusCode}");
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Create Table Error: {ex.Message}");
                ModelState.AddModelError("", "Ett oväntat fel uppstod.");
                return View(model);
            }
        }

        // GET: Admin/EditTable - Visa formulär för redigering
        public async Task<IActionResult> EditTable(int id)
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction("Login");
            }

            try
            {
                var client = _httpClientFactory.CreateClient("RestaurantAPI");
                AddAuthorizationHeader(client);

                Console.WriteLine($"Fetching table {id} for editing...");

                var response = await client.GetAsync($"api/Table/{id}");

                Console.WriteLine($"Response: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    var table = JsonSerializer.Deserialize<Table>(jsonString,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (table == null)
                    {
                        TempData["ErrorMessage"] = "Kunde inte hitta bordet";
                        return RedirectToAction("Tables");
                    }

                    return View(table);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    TempData["ErrorMessage"] = "Din session har gått ut.";
                    return RedirectToAction("Logout");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error: {errorContent}");
                    TempData["ErrorMessage"] = $"Kunde inte hämta bord. Status: {response.StatusCode}";
                    return RedirectToAction("Tables");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Edit Table Get Error: {ex.Message}");
                TempData["ErrorMessage"] = "Ett fel uppstod";
                return RedirectToAction("Tables");
            }
        }

        // POST: Admin/EditTable - Uppdatera bord
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTable(Table model)
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction("Login");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var client = _httpClientFactory.CreateClient("RestaurantAPI");
                AddAuthorizationHeader(client);

                var tableRequest = new
                {
                    id = model.Id,
                    tableNumber = model.TableNumber,
                    capacity = model.Capacity
                };

                var jsonContent = JsonSerializer.Serialize(tableRequest);
                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                Console.WriteLine($"PUT to: api/Table/{model.Id}");
                Console.WriteLine($"Body: {jsonContent}");

                var response = await client.PutAsync($"api/Table/{model.Id}", httpContent);

                Console.WriteLine($"Response: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = $"Bord {model.TableNumber} har uppdaterats!";
                    return RedirectToAction("Tables");
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    TempData["ErrorMessage"] = "Din session har gått ut.";
                    return RedirectToAction("Logout");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"PUT Error: {response.StatusCode} - {errorContent}");
                    ModelState.AddModelError("", $"Kunde inte uppdatera bord. Status: {response.StatusCode}");
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Edit Table Post Error: {ex.Message}");
                ModelState.AddModelError("", "Ett oväntat fel uppstod.");
                return View(model);
            }
        }

        // POST: Admin/DeleteTable - Ta bort bord
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTable(int id)
        {
            if (!IsAdminLoggedIn())
            {
                return RedirectToAction("Login");
            }

            try
            {
                var client = _httpClientFactory.CreateClient("RestaurantAPI");
                AddAuthorizationHeader(client);

                Console.WriteLine($"DELETE: api/Table/{id}");

                var response = await client.DeleteAsync($"api/Table/{id}");

                Console.WriteLine($"Response: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Bordet har tagits bort";
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    TempData["ErrorMessage"] = "Din session har gått ut.";
                    return RedirectToAction("Logout");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"DELETE Error: {response.StatusCode} - {errorContent}");
                    TempData["ErrorMessage"] = "Kunde inte ta bort bord";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Delete Table Error: {ex.Message}");
                TempData["ErrorMessage"] = "Ett fel uppstod";
            }

            return RedirectToAction("Tables");
        }
    }
}
