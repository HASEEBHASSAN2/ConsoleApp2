using System;
using System.Threading.Tasks;

namespace ConsoleApp2
{
    internal class Program
    {
        [STAThread]
        static async Task Main(string[] args)
        {
            Console.WriteLine("==============================================");
            Console.WriteLine("       Scanner Web API Service");
            Console.WriteLine("==============================================\n");

            // Check for test mode
            if (args.Length > 0 && args[0] == "--test")
            {
                Console.WriteLine("Running in TEST mode...\n");
                await SimpleHttpTest.TestHttpListener();
                return;
            }

            try
            {
                // Initialize scanner service
                Console.WriteLine("Initializing scanner service...");
                ScannerService.Initialize();
                Console.WriteLine("Scanner service initialized successfully.\n");

                // Start Web API
                var api = new ScannerWebAPI("http://localhost:5195/");
                await api.StartAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n*** FATAL ERROR ***");
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"\nStack trace:");
                Console.WriteLine(ex.StackTrace);
                
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"\nInner exception: {ex.InnerException.Message}");
                }
                
                Console.WriteLine("\nPress any key to exit...");
                Console.ReadKey();
            }
        }
    }
}
