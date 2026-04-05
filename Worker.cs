using System.Drawing.Printing;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QPrintBridge;

public class Worker : BackgroundService
{
    private readonly string _url = "http://localhost:19100/";

    public Worker()
    {
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        SimpleLogger.LogInfo("QPrintBridge Service iniciado.");

        using var listener = new HttpListener();
        listener.Prefixes.Add(_url);

        try
        {
            listener.Start();
            SimpleLogger.LogInfo($"Escuchando en {_url}");
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError("Error al iniciar HttpListener. ¿Requiere permisos de administrador?", ex);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var contextTask = listener.GetContextAsync();
                
                // Esperar por un contexto o por el token de cancelación
                var tcs = new TaskCompletionSource<bool>();
                using (stoppingToken.Register(s => ((TaskCompletionSource<bool>)s!).TrySetResult(true), tcs))
                {
                    if (contextTask != await Task.WhenAny(contextTask, tcs.Task))
                    {
                        break; // Se solicitó cancelación
                    }
                }

                var context = await contextTask;
                _ = Task.Run(() => ProcessRequestAsync(context, stoppingToken), stoppingToken);
            }
            catch (HttpListenerException)
            {
                // listener deteniéndose u otro error de red, salir del bucle
                break;
            }
            catch (Exception ex)
            {
                SimpleLogger.LogError("Error en el bucle principal de escucha.", ex);
            }
        }

        listener.Stop();
        SimpleLogger.LogInfo("QPrintBridge Service detenido.");
    }

    private async Task ProcessRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            // Configurar CORS
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

            // Manejar Preflight (OPTIONS)
            if (request.HttpMethod.ToUpper() == "OPTIONS")
            {
                response.StatusCode = (int)HttpStatusCode.OK;
                response.Close();
                return;
            }

            string responseBody = "";
            int statusCode = (int)HttpStatusCode.OK;
            string path = request.Url?.LocalPath.ToLower() ?? "";

            if (request.HttpMethod.ToUpper() == "GET" && path == "/printers")
            {
                var printers = new List<string>();
                foreach (string printer in PrinterSettings.InstalledPrinters)
                {
                    printers.Add(printer);
                }

                var resultInfo = new { status = "success", printers = printers };
                responseBody = JsonSerializer.Serialize(resultInfo);
            }
            else if (request.HttpMethod.ToUpper() == "POST" && path == "/imprimir")
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                string jsonBody = await reader.ReadToEndAsync(cancellationToken);

                var printRequest = JsonSerializer.Deserialize<PrintPayload>(jsonBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (printRequest == null || string.IsNullOrWhiteSpace(printRequest.Impresora) || string.IsNullOrWhiteSpace(printRequest.Payload))
                {
                    statusCode = (int)HttpStatusCode.BadRequest;
                    responseBody = JsonSerializer.Serialize(new { status = "error", message = "Faltan parámetros 'impresora' o 'payload'." });
                }
                else
                {
                    try
                    {
                        byte[] rawData = Convert.FromBase64String(printRequest.Payload);
                        bool ok = RawPrinterHelper.SendBytesToPrinter(printRequest.Impresora, rawData);

                        if (ok)
                        {
                            responseBody = JsonSerializer.Serialize(new { status = "success", message = "Impreso", printer = printRequest.Impresora });
                        }
                        else
                        {
                            statusCode = (int)HttpStatusCode.InternalServerError;
                            responseBody = JsonSerializer.Serialize(new { status = "error", message = "Fallo al enviar a la cola de impresión." });
                        }
                    }
                    catch (FormatException ex)
                    {
                        SimpleLogger.LogError("Error decodificando Base64.", ex);
                        statusCode = (int)HttpStatusCode.BadRequest;
                        responseBody = JsonSerializer.Serialize(new { status = "error", message = "El payload no es un Base64 válido." });
                    }
                }
            }
            else
            {
                statusCode = (int)HttpStatusCode.NotFound;
                responseBody = JsonSerializer.Serialize(new { status = "error", message = "Endpoint interconectado no encontrado." });
            }

            // Enviar la respuesta
            response.StatusCode = statusCode;
            response.ContentType = "application/json";
            
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseBody);
            response.ContentLength64 = buffer.Length;
            
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length, cancellationToken);
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError($"Error al procesar la petición {request.Url}", ex);
            response.StatusCode = (int)HttpStatusCode.InternalServerError;
            try
            {
                byte[] errorBuffer = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { status = "error", message = "Error interno del servidor." }));
                response.ContentLength64 = errorBuffer.Length;
                await response.OutputStream.WriteAsync(errorBuffer, 0, errorBuffer.Length, cancellationToken);
            }
            catch { /* ignorar si ya no se puede escribir */ }
        }
        finally
        {
            response.Close();
        }
    }
}

public class PrintPayload
{
    [JsonPropertyName("impresora")]
    public string? Impresora { get; set; }

    [JsonPropertyName("payload")]
    public string? Payload { get; set; }
}
