using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Server_PaymentForm_.NET.Models;
using Microsoft.AspNetCore.Cors;

namespace Server.NET.Controllers
{
    [EnableCors("AllowAll")]
    public class McwController : Controller
    {
        private readonly IConfiguration _configuration;

        public McwController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // @@ Manejo de solicitudes POST para checkout @@
        [HttpPost("/formToken")]
        public async Task<IActionResult> Checkout([FromBody] PaymentRequest paymentRequest)
        {
            // Obteniendo claves API
            var apiCredentials = _configuration.GetSection("ApiCredentials");
            string username = apiCredentials["USERNAME"];
            string password = apiCredentials["PASSWORD"];
            string publickey = apiCredentials["PUBLIC_KEY"];

            string url = "https://api.micuentaweb.pe/api-payment/V4/Charge/CreatePayment";
            string auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));

            // Almacenar los valores para el formulario de pago
            var data = new
            {
                amount = Math.Round(paymentRequest.Amount * 100.0, 0),
                currency = paymentRequest.Currency,
                customer = new
                {
                    email = paymentRequest.Email,
                    billingDetails = new
                    {
                        firstName = paymentRequest.FirstName,
                        lastName = paymentRequest.LastName,
                        identityType = paymentRequest.IdentityType,
                        identityCode = paymentRequest.IdentityCode,
                        phoneNumber = paymentRequest.PhoneNumber,
                        address = paymentRequest.Address,
                        country = paymentRequest.Country,
                        state = paymentRequest.State,
                        city = paymentRequest.City,
                        zipCode = paymentRequest.ZipCode
                    }
                },
                orderId = paymentRequest.OrderId
            };


            // Crear la conexión a la API para la creación del FormToken
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await client.PostAsync(url, new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json"));
                var responseContent = await response.Content.ReadAsStringAsync();

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var responseData = JsonSerializer.Deserialize<PaymentResponse>(responseContent, options);

                // Extrae el FormToken y PublicKey
                if (responseData.Status == "SUCCESS")
                {
                    var jsonResponse = new { formToken = responseData?.Answer?.FormToken, publicKey = publickey };
                    return Ok(jsonResponse);

                }
                else
                {
                    var errorResponse = new { Error = "Error al generar el FormToken", Code = 422 };
                    return StatusCode(422, errorResponse);
                }
            }
        }

        // @@ Manejo de solicitudes POST para result @@
        [HttpPost("/validate")]
        public IActionResult Result([FromBody] JsonElement jsonData)
        {
            string hmacKey = _configuration.GetSection("ApiCredentials")["HMACSHA256"];

            // Válida que la respuesta sea íntegra comprando el hash recibido en el 'kr-hash' con el generado con el 'kr-answer'
            if (!CheckHash(jsonData, hmacKey))
            {
                var errorResponse = new { Error = "Invalid signature", Code = 422 };
                return StatusCode(422, errorResponse);
            }


            return Ok(true);
        }

        [HttpPost("/ipn")]
        public IActionResult Ipn([FromForm] IFormCollection form)
        {
            string privateKey = _configuration.GetSection("ApiCredentials")["PASSWORD"];

            // Válida que la respuesta sea íntegra comprando el hash recibido en el 'kr-hash' con el generado con el 'kr-answer'
            if (!CheckHash(form, privateKey))
            {
                Console.WriteLine("Invalid signature");
                return View("Error");
            }  

            string krAnswer = form["kr-answer"].ToString();

            using var jsonDocument = JsonDocument.Parse(krAnswer);
            var root = jsonDocument.RootElement;

            // Extrae datos de la transacción
            string orderStatus = root.GetProperty("orderStatus").GetString();
            string orderId = root.GetProperty("orderDetails").GetProperty("orderId").GetString();
            string uuid = root.GetProperty("transactions")[0].GetProperty("uuid").GetString();

            // Retorna el valor de OrderStatus
            return Ok($"OK! Order Status: {orderStatus}");

        }

        // Verifica la integridad del Hash recibido y el generado  	
        private bool CheckHash(dynamic formData, string key)
        {
            // Extrae el kr-answer y kr-hash
            string answer = formData is IFormCollection form
                ? form["kr-answer"].ToString()
                : formData.GetProperty("kr-answer").GetString();

            string hash = formData is IFormCollection formCollection
                ? formCollection["kr-hash"].ToString()
                : formData.GetProperty("kr-hash").GetString();

            // Verificar que los valores existen
            if (string.IsNullOrEmpty(answer) || string.IsNullOrEmpty(hash))
            {
                Console.WriteLine("Missing required fields");
                return false;
            }

            // Genera un hash HMAC-SHA256
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key)))
            {
                byte[] answerBytes = Encoding.UTF8.GetBytes(answer);
                byte[] computedHashBytes = hmac.ComputeHash(answerBytes);
                string computedHashString = Convert.ToHexString(computedHashBytes).ToLowerInvariant();

                return string.Equals(computedHashString, hash, StringComparison.OrdinalIgnoreCase);
            }
        }

    }
}
