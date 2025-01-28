<p align="center">
  <img src="https://github.com/izipay-pe/Imagenes/blob/main/logos_izipay/logo-izipay-banner-1140x100.png?raw=true" alt="Formulario" width=100%/>
</p>

# Server-PaymentForm-.NET

## Índice

➡️ [1. Introducción](#-1-introducci%C3%B3n)  
🔑 [2. Requisitos previos](#-2-requisitos-previos)  
🚀 [3. Ejecutar ejemplo](#-3-ejecutar-ejemplo)  
🔗 [4. APIs](#4-APIs)  
💻 [4.1. FormToken](#41-formtoken)  
💳 [4.2. Validación de firma](#42-validaci%C3%B3n-de-firma)  
📡 [4.3. IPN](#43-ipn)  
📮 [5. Probar desde POSTMAN](#-5-probar-desde-postman)  
📚 [6. Consideraciones](#-6-consideraciones)

## ➡️ 1. Introducción

En este manual podrás encontrar una guía paso a paso para configurar un servidor API REST (Backend) en **[.NET]** para la pasarela de pagos de IZIPAY. **El actual proyecto no incluye una interfaz de usuario (Frontend)** y debe integrarse con un proyecto de Front. Te proporcionaremos instrucciones detalladas y credenciales de prueba para la instalación y configuración del proyecto, permitiéndote trabajar y experimentar de manera segura en tu propio entorno local.
Este manual está diseñado para ayudarte a comprender el flujo de la integración de la pasarela para ayudarte a aprovechar al máximo tu proyecto y facilitar tu experiencia de desarrollo.

<p align="center">
  <img src="https://i.postimg.cc/KYpyqYPn/imagen-2025-01-28-082121144.png" alt="Formulario"/>
</p>

## 🔑 2. Requisitos Previos

- Comprender el flujo de comunicación de la pasarela. [Información Aquí](https://secure.micuentaweb.pe/doc/es-PE/rest/V4.0/javascript/guide/start.html)
- Extraer credenciales del Back Office Vendedor. [Guía Aquí](https://github.com/izipay-pe/obtener-credenciales-de-conexion)
- Para este proyecto utilizamos .NET 8.0.
- Visual Studio
- 
> [!NOTE]
> Tener en cuenta que, para que el desarrollo de tu proyecto, eres libre de emplear tus herramientas preferidas.

## 🚀 3. Ejecutar ejemplo


### Clonar el proyecto
```sh
git clone https://github.com/izipay-pe/Embedded-PaymentForm-.NET
``` 

### Datos de conexión 

Reemplace **[CHANGE_ME]** con sus credenciales de `API REST` extraídas desde el Back Office Vendedor, revisar [Requisitos previos](#-2-requisitos-previos).

- Editar el archivo `appsettings.json` en la ruta raíz:
```json
"ApiCredentials": {
        "USERNAME": "CHANGE_ME_USER_ID",
        "PASSWORD": "CHANGE_ME_PASSWORD",
        "PUBLIC_KEY": "CHANGE_ME_PUBLIC_KEY",
        "HMACSHA256": "CHANGE_ME_HMAC_SHA_256"
    }
```

### Ejecutar proyecto

1. Una vez dentro del código ejecutamos el proyecto con el comando F5 y se abrirá en tu navegador predeterminado accediendo a la siguiente ruta:

```sh
https://localhost:7030/
``` 


## 🔗4. APIs
- 💻 **FormToken:** Generación de formToken y envío de la llave publicKey necesarios para desplegar la pasarela.
- 💳  **Validacion de firma:** Se encarga de verificar la autenticidad de los datos.
- 📩 ️ **IPN:** Comunicación de servidor a servidor. Envío de los datos del pago al servidor.

## 💻4.1. FormToken
Para configurar la pasarela se necesita generar un formtoken. Se realizará una solicitud API REST a la api de creación de pagos:  `https://api.micuentaweb.pe/api-payment/V4/Charge/CreatePayment` con los datos de la compra para generar el formtoken. El servidor devuelve el formToken generado junto a la llave `publicKey` necesaria para desplegar la pasarela

Podrás encontrarlo en el archivo `Controllers/McwController.cs`.

```c#
        [HttpPost("/formToken")]
        public async Task<IActionResult> Checkout([FromBody] PaymentRequest paymentRequest)
        {
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
                ...
                ...
                orderId = paymentRequest.OrderId
            };

            // Crear la conexión a la API para la creación del FormToken
            using (HttpClient client = new HttpClient())
            {
               
                // Extrae el FormToken y PublicKey
                if (responseData.Status == "SUCCESS")
                {
                    var jsonResponse = new { formToken = responseData?.Answer?.FormToken, publicKey = publickey };
                    return Ok(jsonResponse);

                }
                ...
                ...
        }
```

Podrás acceder a esta API a través:

```bash
localhost:7030/formToken
```

ℹ️ Para más información: [Formtoken](https://secure.micuentaweb.pe/doc/es-PE/rest/V4.0/javascript/guide/embedded/formToken.html)

## 💳4.2. Validación de firma
Se configura la función `checkHash` que realizará la validación de los datos recibidos por el servidor luego de realizar el pago mediante el parámetro `kr-answer` utilizando una clave de encriptación definida en `key`. Podrás encontrarlo en el archivo `Controllers/McwController.cs`.

```c#
        private bool CheckHash(dynamic formData, string key)
        {
            ...
            ..
            // Genera un hash HMAC-SHA256
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key)))
            {
                byte[] answerBytes = Encoding.UTF8.GetBytes(answer);
                byte[] computedHashBytes = hmac.ComputeHash(answerBytes);
                string computedHashString = Convert.ToHexString(computedHashBytes).ToLowerInvariant();
                return string.Equals(computedHashString, hash, StringComparison.OrdinalIgnoreCase);
            }
        }
```

Se valida que la firma recibida es correcta. Para la validación de los datos recibidos a través de la pasarela de pagos (front) se utiliza la clave `HMACSHA256`.

```c#
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
}
```
El servidor devuelve un valor booleano `true` verificando si los datos de la transacción coinciden con la firma recibida. Se confirma que los datos son enviados desde el servidor de Izipay.

Podrás acceder a esta API a través:
```bash
localhost:7030/validate
```

ℹ️ Para más información: [Analizar resultado del pago](https://secure.micuentaweb.pe/doc/es-PE/rest/V4.0/kb/payment_done.html)

## 📩4.3. IPN
La IPN es una notificación de servidor a servidor (servidor de Izipay hacia el servidor del comercio) que facilita información en tiempo real y de manera automática cuando se produce un evento, por ejemplo, al registrar una transacción.

Se realiza la verificación de la firma utilizando la función `checkHash`. Para la validación de los datos recibidos a través de la IPN (back) se utiliza la clave `PASSWORD`. Se devuelve al servidor de izipay un mensaje confirmando el estado del pago.

Se recomienda verificar el parámetro `orderStatus` para determinar si su valor es `PAID` o `UNPAID`. De esta manera verificar si el pago se ha realizado con éxito.

Podrás encontrarlo en el archivo `Controllers/McwController.cs`.

```c#
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
            ...
            ...
            // Extrae datos de la transacción
            string orderStatus = root.GetProperty("orderStatus").GetString();
            ...
            ...
            // Retorna el valor de OrderStatus
            return Ok($"OK! Order Status: {orderStatus}");

        }
```
Podrás acceder a esta API a través:
```bash
localhost:7030/ipn
```

La ruta o enlace de la IPN debe ir configurada en el Backoffice Vendedor, en `Configuración -> Reglas de notificación -> URL de notificación al final del pago`

<p align="center">
  <img src="https://i.postimg.cc/XNGt9tyt/ipn.png" alt="Formulario" width=80%/>
</p>

ℹ️ Para más información: [Analizar IPN](https://secure.micuentaweb.pe/doc/es-PE/rest/V4.0/api/kb/ipn_usage.html)

## 📡4.3.Pase a producción

Reemplace **[CHANGE_ME]** con sus credenciales de PRODUCCIÓN de `API REST` extraídas desde el Back Office Vendedor, revisar [Requisitos Previos](#-2-requisitos-previos).

- Editar el archivo `appsettings.json` en la ruta raíz:
```json
"ApiCredentials": {
        "USERNAME": "CHANGE_ME_USER_ID",
        "PASSWORD": "CHANGE_ME_PASSWORD",
        "PUBLIC_KEY": "CHANGE_ME_PUBLIC_KEY",
        "HMACSHA256": "CHANGE_ME_HMAC_SHA_256"
    }
```

## 📮 5. Probar desde POSTMAN
* Puedes probar la generación del formToken desde POSTMAN. Coloca la URL con el metodo POST con la ruta `/formToken`.
  
 ```bash
localhost:7030/formToken
```

* Datos a enviar en formato JSON raw:
 ```node
{
    "amount": 1000,
    "currency": "PEN", //USD
    "orderId": "ORDER12345",
    "email": "cliente@example.com",
    "firstName": "John",
    "lastName": "Doe",
    "phoneNumber": "123456789",
    "identityType": "DNI",
    "identityCode": "ABC123456",
    "address": "Calle principal 123",
    "country": "PE",
    "city": "Lima",
    "state": "Lima",
    "zipCode": "10001"
}
```

## 📚 6. Consideraciones

Para obtener más información, echa un vistazo a:

- [Formulario incrustado: prueba rápida](https://secure.micuentaweb.pe/doc/es-PE/rest/V4.0/javascript/quick_start_js.html)
- [Primeros pasos: pago simple](https://secure.micuentaweb.pe/doc/es-PE/rest/V4.0/javascript/guide/start.html)
- [Servicios web - referencia de la API REST](https://secure.micuentaweb.pe/doc/es-PE/rest/V4.0/api/reference.html)
