using Microsoft.AspNetCore.Mvc;
using MVCResturang.Models;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;

namespace MVCResturang.Controllers
{
    public class BookingController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public BookingController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public IActionResult Index()
        {
            var model = new BookingViewModel
            {
                Date = DateTime.Today,
                Time = new TimeSpan(18, 0, 0)
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(BookingViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (model.Date.Date < DateTime.Today)
            {
                ModelState.AddModelError("Date", "Du kan inte boka ett datum som redan passerat");
                return View(model);
            }

            if (model.Time < new TimeSpan(11, 0, 0) || model.Time > new TimeSpan(22, 0, 0))
            {
                ModelState.AddModelError("Time", "Bokningar är endast möjliga mellan 11:00 och 22:00");
                return View(model);
            }

            try
            {
                var client = _httpClientFactory.CreateClient("RestaurantAPI");

                // STEG 1: Skapa customer först
                var customerRequest = new
                {
                    name = model.Name,
                    email = model.Email,
                    phoneNumber = model.Phone
                };

                var customerJson = JsonSerializer.Serialize(customerRequest);
                var customerContent = new StringContent(customerJson, Encoding.UTF8, "application/json");

                Console.WriteLine($"Creating customer: {customerJson}");

                var customerResponse = await client.PostAsync("api/Customer", customerContent);

                if (!customerResponse.IsSuccessStatusCode)
                {
                    var errorContent = await customerResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"Customer creation failed: {customerResponse.StatusCode} - {errorContent}");
                    ModelState.AddModelError("", "Kunde inte skapa kund. Försök igen.");
                    return View(model);
                }

                var customerResponseContent = await customerResponse.Content.ReadAsStringAsync();
                using JsonDocument customerDoc = JsonDocument.Parse(customerResponseContent);
                JsonElement customerRoot = customerDoc.RootElement;

                int customerId = 0;
                if (customerRoot.TryGetProperty("id", out JsonElement customerIdElement))
                {
                    customerId = customerIdElement.GetInt32();
                }

                Console.WriteLine($"Customer created with ID: {customerId}");

                // STEG 2: Fråga API:t efter ett föreslaget bord (skicka JWT om tillgänglig)
                var bookingDateTime = model.Date.Add(model.Time);
                var suggestUrl =
                    $"api/Table/suggest?bookingTime={Uri.EscapeDataString(bookingDateTime.ToString("o"))}&guests={model.NumberOfGuests}";

                // Lägg till Authorization-header om token finns i session
                var token = HttpContext.Session.GetString("JwtToken");
                if (!string.IsNullOrEmpty(token))
                {
                    client.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                Console.WriteLine($"Requesting suggested table: {suggestUrl}");
                var tableResponse = await client.GetAsync(suggestUrl);

                // Logga svar för felsökning
                var tableResponseBody = await tableResponse.Content.ReadAsStringAsync();
                Console.WriteLine($"Table suggest response: {tableResponse.StatusCode}");
                Console.WriteLine($"Table suggest body: {tableResponseBody}");

                if (!tableResponse.IsSuccessStatusCode)
                {
                    if (tableResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        ModelState.AddModelError("", "Kräver inloggning för att hämta ledigt bord (401). Logga in och försök igen.");
                    }
                    else
                    {
                        ModelState.AddModelError("", $"Inget ledigt bord hittades eller fel på API: {tableResponse.StatusCode}. {tableResponseBody}");
                    }

                    return View(model);
                }

                // Deserialisera föreslaget bord
                Table? candidate = null;
                try
                {
                    candidate = JsonSerializer.Deserialize<Table>(tableResponseBody,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"Failed to parse suggested table JSON: {ex.Message}");
                }

                if (candidate == null)
                {
                    ModelState.AddModelError("", "Kunde inte hitta ett ledigt bord från API.");
                    return View(model);
                }

                int tableId = candidate.Id;
                Console.WriteLine(
                    $"Selected tableNumber {candidate.TableNumber} with capacity {candidate.Capacity} " +
                    $"for {model.NumberOfGuests} guests at {bookingDateTime}");

                // STEG 3: Skapa booking med riktiga IDs
                var bookingRequest = new
                {
                    bookingTime = bookingDateTime,
                    numberOfGuests = model.NumberOfGuests,
                    customerId = customerId,
                    tableId = tableId
                };

                var bookingJson = JsonSerializer.Serialize(bookingRequest);
                var bookingContent = new StringContent(bookingJson, Encoding.UTF8, "application/json");

                Console.WriteLine($"Creating booking: {bookingJson}");

                var bookingResponse = await client.PostAsync("api/Booking", bookingContent);

                if (bookingResponse.IsSuccessStatusCode)
                {
                    var bookingResponseContent = await bookingResponse.Content.ReadAsStringAsync();

                    using JsonDocument doc = JsonDocument.Parse(bookingResponseContent);
                    JsonElement root = doc.RootElement;

                    int bookingId = 0;
                    if (root.TryGetProperty("id", out JsonElement idElement))
                    {
                        bookingId = idElement.GetInt32();
                    }

                    TempData["SuccessMessage"] = $"Tack {model.Name}! Din bokning är bekräftad för {model.Date:yyyy-MM-dd} kl {model.Time:hh\\:mm}.";
                    TempData["BookingId"] = bookingId;

                    return RedirectToAction("Confirmation");
                }
                else
                {
                    var errorContent = await bookingResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"Booking Error: {bookingResponse.StatusCode} - {errorContent}");

                    // Visa enkel och användarvänlig text istället för rå status/body
                    if (bookingResponse.StatusCode == System.Net.HttpStatusCode.BadRequest)
                    {
                        // Om API:s svar innehåller ett förklarande meddelande kan vi visa en kort variant
                        ModelState.AddModelError("", "Ogiltig bokning. Kontrollera dina uppgifter och försök igen.");
                    }
                    else if (bookingResponse.StatusCode == System.Net.HttpStatusCode.Conflict)
                    {
                        ModelState.AddModelError("", "Tyvärr, bordet är upptaget. Välj en annan tid eller minska antal gäster.");
                    }
                    else if (bookingResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        ModelState.AddModelError("", "Du måste vara inloggad för att skapa bokningar. Logga in och försök igen.");
                    }
                    else
                    {
                        // Generiskt fel till användaren — behåll detaljer i loggarna
                        ModelState.AddModelError("", "Kunde inte skapa bokning just nu. Försök igen senare.");
                    }

                    return View(model);
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Network Error: {ex.Message}");
                ModelState.AddModelError("", "Kunde inte ansluta till bokningssystemet. Kontrollera att API:t körs.");
                return View(model);
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"JSON Error: {ex.Message}");
                ModelState.AddModelError("", "Fel vid bearbetning av bokningsdata.");
                return View(model);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected Error: {ex.Message}");
                ModelState.AddModelError("", "Ett oväntat fel uppstod.");
                return View(model);
            }
        }

        public IActionResult Confirmation()
        {
            if (TempData["SuccessMessage"] == null)
            {
                return RedirectToAction("Index");
            }

            ViewBag.Message = TempData["SuccessMessage"];
            ViewBag.BookingId = TempData["BookingId"];

            return View(); 
        }
       

    }
}