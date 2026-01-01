using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ConsoleApp2
{
    public class ScannerWebAPI
    {
        private HttpListener _listener;
        private readonly string _outputPath;
        private readonly string _baseUrl;

        public ScannerWebAPI(string baseUrl = "http://localhost:5195/", string outputPath = null)
        {
            _baseUrl = baseUrl;
            _outputPath = outputPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Scans");
            Directory.CreateDirectory(_outputPath);
        }

        public async Task StartAsync()
        {
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add(_baseUrl);
                _listener.Start();

                Console.WriteLine($"Scanner Web API started on {_baseUrl}");
                Console.WriteLine($"Scan images will be saved to: {_outputPath}");
                Console.WriteLine("\nAvailable endpoints:");
                Console.WriteLine($"  GET  {_baseUrl}api/scanners - List available scanners");
                Console.WriteLine($"  POST {_baseUrl}api/scan - Start scanning");
                Console.WriteLine($"  GET  {_baseUrl}api/images/{{filename}} - Get scanned image");
                Console.WriteLine("\nPress Ctrl+C to stop the server...");
                Console.WriteLine("Waiting for requests...\n");

                while (_listener.IsListening)
                {
                    try
                    {
                        Console.WriteLine("DEBUG: Waiting for connection...");
                        var contextTask = _listener.GetContextAsync();
                        
                        // Process Windows Forms messages while waiting
                        while (!contextTask.IsCompleted)
                        {
                            System.Windows.Forms.Application.DoEvents();
                            await Task.Delay(10);
                        }
                        
                        var context = await contextTask;
                        Console.WriteLine($"DEBUG: Received {context.Request.HttpMethod} request to {context.Request.Url.AbsolutePath}");
                        
                        _ = Task.Run(() => HandleRequest(context));
                    }
                    catch (HttpListenerException ex) when (ex.ErrorCode == 995) // Operation aborted
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (_listener.IsListening)
                        {
                            Console.WriteLine($"Error accepting connection: {ex.Message}");
                            Console.WriteLine($"Stack trace: {ex.StackTrace}");
                        }
                    }
                }
            }
            catch (HttpListenerException ex)
            {
                Console.WriteLine($"\n*** HTTP Listener Error ***");
                Console.WriteLine($"Error Code: {ex.ErrorCode}");
                Console.WriteLine($"Message: {ex.Message}");
                
                if (ex.ErrorCode == 5) // Access Denied
                {
                    Console.WriteLine("\n*** ACCESS DENIED ***");
                    Console.WriteLine("Please run this application as Administrator!");
                    Console.WriteLine("Right-click the .exe and select 'Run as administrator'");
                }
                else if (ex.ErrorCode == 183) // Already in use
                {
                    Console.WriteLine("\n*** PORT ALREADY IN USE ***");
                    Console.WriteLine($"Another application is using port 5195.");
                    Console.WriteLine("Try closing other applications or use a different port.");
                }
                
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n*** Unexpected Error ***");
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        private async Task HandleRequest(HttpListenerContext context)
        {
            try
            {
                Console.WriteLine($"Handling request: {context.Request.HttpMethod} {context.Request.Url.AbsolutePath}");
                
                // Enable CORS
                context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

                if (context.Request.HttpMethod == "OPTIONS")
                {
                    Console.WriteLine("Handling OPTIONS (CORS preflight)");
                    context.Response.StatusCode = 200;
                    context.Response.Close();
                    return;
                }

                var path = context.Request.Url.AbsolutePath.ToLower();

                if (path == "/api/scanners" && context.Request.HttpMethod == "GET")
                {
                    await HandleGetScannersAsync(context);
                }
                else if (path == "/api/scan" && context.Request.HttpMethod == "POST")
                {
                    await HandleScanAsync(context);
                }
                else if (path.StartsWith("/api/images/") && context.Request.HttpMethod == "GET")
                {
                    await HandleGetImageAsync(context);
                }
                else
                {
                    Console.WriteLine($"404 Not Found: {path}");
                    context.Response.StatusCode = 404;
                    await SendJsonResponse(context, new { error = "Endpoint not found" });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Request error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                try
                {
                    context.Response.StatusCode = 500;
                    await SendJsonResponse(context, new { error = ex.Message });
                }
                catch
                {
                    // Response already closed
                }
            }
        }

        private async Task HandleGetScannersAsync(HttpListenerContext context)
        {
            Console.WriteLine("GET /api/scanners - Listing available scanners");

            try
            {
                var scanners = ScannerService.GetScanners();
                Console.WriteLine($"Found {scanners.Count} scanner(s)");
                
                await SendJsonResponse(context, new
                {
                    success = true,
                    scanners = scanners
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting scanners: {ex.Message}");
                context.Response.StatusCode = 500;
                await SendJsonResponse(context, new
                {
                    success = false,
                    error = ex.Message
                });
            }
        }

        private async Task HandleScanAsync(HttpListenerContext context)
        {
            Console.WriteLine("POST /api/scan - Starting scan operation");

            try
            {
                // Read request body
                string requestBody;
                using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
                {
                    requestBody = await reader.ReadToEndAsync();
                }

                var scanRequest = JsonConvert.DeserializeObject<ScanRequest>(requestBody);

                if (string.IsNullOrEmpty(scanRequest?.ScannerId))
                {
                    context.Response.StatusCode = 400;
                    await SendJsonResponse(context, new
                    {
                        success = false,
                        error = "ScannerId is required"
                    });
                    return;
                }

                Console.WriteLine($"Scanning with scanner: {scanRequest.ScannerId}");

                var result = await ScannerService.ScanAsync(scanRequest.ScannerId, _outputPath);

                if (result.Success)
                {
                    Console.WriteLine($"Scan successful: {result.FileName}");
                    
                    await SendJsonResponse(context, new
                    {
                        success = true,
                        imagePath = result.ImagePath,
                        fileName = result.FileName,
                        imageUrl = $"{_baseUrl}api/images/{result.FileName}"
                    });
                }
                else
                {
                    Console.WriteLine($"Scan failed: {result.ErrorMessage}");
                    context.Response.StatusCode = 500;
                    await SendJsonResponse(context, new
                    {
                        success = false,
                        error = result.ErrorMessage
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Scan error: {ex.Message}");
                context.Response.StatusCode = 500;
                await SendJsonResponse(context, new
                {
                    success = false,
                    error = ex.Message
                });
            }
        }

        private async Task HandleGetImageAsync(HttpListenerContext context)
        {
            var fileName = Path.GetFileName(context.Request.Url.AbsolutePath);
            var filePath = Path.Combine(_outputPath, fileName);

            Console.WriteLine($"GET /api/images/{fileName}");

            if (!File.Exists(filePath))
            {
                context.Response.StatusCode = 404;
                await SendJsonResponse(context, new { error = "Image not found" });
                return;
            }

            try
            {
                var imageBytes = File.ReadAllBytes(filePath);
                context.Response.ContentType = "image/jpeg";
                context.Response.ContentLength64 = imageBytes.Length;
                await context.Response.OutputStream.WriteAsync(imageBytes, 0, imageBytes.Length);
                context.Response.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error serving image: {ex.Message}");
                context.Response.StatusCode = 500;
                await SendJsonResponse(context, new { error = ex.Message });
            }
        }

        private async Task SendJsonResponse(HttpListenerContext context, object data)
        {
            context.Response.ContentType = "application/json";
            var json = JsonConvert.SerializeObject(data, Formatting.Indented);
            var buffer = Encoding.UTF8.GetBytes(json);
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            context.Response.Close();
        }

        public void Stop()
        {
            _listener?.Stop();
            _listener?.Close();
        }
    }

    public class ScanRequest
    {
        public string ScannerId { get; set; }
    }
}
