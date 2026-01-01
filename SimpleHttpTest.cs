using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp2
{
    public class SimpleHttpTest
    {
        public static async Task TestHttpListener()
        {
            Console.WriteLine("Testing HttpListener functionality...\n");

            try
            {
                var listener = new HttpListener();
                listener.Prefixes.Add("http://localhost:5195/");
                
                Console.WriteLine("Starting listener...");
                listener.Start();
                Console.WriteLine("? Listener started successfully!");
                Console.WriteLine($"? Listening on: http://localhost:5195/");
                Console.WriteLine("\nTry accessing: http://localhost:5195/test in your browser");
                Console.WriteLine("Press Ctrl+C to stop\n");

                while (true)
                {
                    Console.WriteLine("Waiting for request...");
                    var context = await listener.GetContextAsync();
                    
                    Console.WriteLine($"? Received: {context.Request.HttpMethod} {context.Request.Url}");
                    Console.WriteLine($"  User Agent: {context.Request.UserAgent}");
                    Console.WriteLine($"  Remote IP: {context.Request.RemoteEndPoint}");
                    
                    var response = "Hello from Scanner API Test!";
                    var buffer = Encoding.UTF8.GetBytes(response);
                    
                    context.Response.ContentType = "text/plain";
                    context.Response.ContentLength64 = buffer.Length;
                    context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                    
                    await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    context.Response.Close();
                    
                    Console.WriteLine("? Response sent\n");
                }
            }
            catch (HttpListenerException ex)
            {
                Console.WriteLine($"\n? HttpListener Error:");
                Console.WriteLine($"  Error Code: {ex.ErrorCode}");
                Console.WriteLine($"  Message: {ex.Message}");
                
                if (ex.ErrorCode == 5)
                {
                    Console.WriteLine("\n*** You need to run as Administrator! ***");
                    Console.WriteLine("Right-click the .exe and select 'Run as administrator'");
                }
                else if (ex.ErrorCode == 183)
                {
                    Console.WriteLine("\n*** Port 5195 is already in use! ***");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n? Error: {ex.Message}");
                Console.WriteLine($"Stack: {ex.StackTrace}");
            }
        }
    }
}
