using NTwain;
using NTwain.Data;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ConsoleApp2
{
    public class ScannerService
    {
        private static Form _messageLoopForm;
        private static TwainSession _session;
        private static bool _isInitialized = false;
        private static readonly object _lock = new object();
        private static Thread _staThread;
        private static SynchronizationContext _syncContext;

        public static void Initialize()
        {
            lock (_lock)
            {
                if (_isInitialized) return;

                var initEvent = new ManualResetEvent(false);

                _staThread = new Thread(() =>
                {
                    try
                    {
                        // Set SynchronizationContext FIRST before any TWAIN calls
                        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
                        _syncContext = SynchronizationContext.Current;

                        _messageLoopForm = new Form();
                        _messageLoopForm.ShowInTaskbar = false;
                        _messageLoopForm.WindowState = FormWindowState.Minimized;
                        _messageLoopForm.Opacity = 0;
                        
                        // Create control handle
                        var handle = _messageLoopForm.Handle; // Force handle creation
                        
                        // Now initialize TWAIN
                        var appId = TWIdentity.CreateFromAssembly(DataGroups.Image, System.Reflection.Assembly.GetExecutingAssembly());
                        _session = new TwainSession(appId);
                        
                        var openResult = _session.Open();
                        if (openResult != ReturnCode.Success)
                        {
                            throw new Exception($"Failed to open TWAIN session: {openResult}");
                        }

                        _isInitialized = true;
                        initEvent.Set();

                        // Run message loop
                        Application.Run(_messageLoopForm);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"? TWAIN initialization error: {ex.Message}");
                        initEvent.Set();
                    }
                });

                _staThread.SetApartmentState(ApartmentState.STA);
                _staThread.IsBackground = true;
                _staThread.Name = "TWAIN_STA_Thread";
                _staThread.Start();

                // Wait for initialization with proper event
                if (!initEvent.WaitOne(TimeSpan.FromSeconds(10)))
                {
                    throw new Exception("TWAIN initialization timeout");
                }

                if (!_isInitialized)
                {
                    throw new Exception("Failed to initialize TWAIN scanner service");
                }
            }
        }

        public static List<ScannerInfo> GetScanners()
        {
            Initialize();

            var tcs = new TaskCompletionSource<List<ScannerInfo>>();

            _syncContext.Post(_ =>
            {
                try
                {
                    var scanners = new List<ScannerInfo>();
                    var sources = _session.ToList();

                    foreach (var source in sources)
                    {
                        scanners.Add(new ScannerInfo
                        {
                            Id = source.Name,
                            Name = source.Name,
                            Manufacturer = source.Manufacturer,
                            ProductName = source.ProductFamily
                        });
                    }

                    tcs.SetResult(scanners);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }, null);

            return tcs.Task.Result;
        }

        public static Task<ScanResult> ScanAsync(string scannerId, string outputPath)
        {
            Initialize();

            var tcs = new TaskCompletionSource<ScanResult>();

            _syncContext.Post(_ =>
            {
                try
                {
                    var result = PerformScan(scannerId, outputPath);
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }, null);

            return tcs.Task;
        }

        public static Task<ScanResult> ScanSimpleAsync(string scannerId, string outputPath)
        {
            var tcs = new TaskCompletionSource<ScanResult>();

            // Run in background with TWAIN to trigger, then monitor files
            Task.Run(() =>
            {
                try
                {
                    var result = PerformSimpleScanWithAutoTrigger(scannerId, outputPath);
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            return tcs.Task;
        }

        private static ScanResult PerformScan(string scannerId, string outputPath)
        {
            var result = new ScanResult();
            var scanComplete = false;
            var dataReceived = false;
            Exception scanException = null;
            DataSource src = null;
            bool sourceOpened = false;

            EventHandler<DataTransferredEventArgs> dataTransferredHandler = (s, e) =>
            {
                Console.WriteLine("? DATA RECEIVED");
                try
                {
                    IntPtr handle = e.NativeData;
                    if (handle != IntPtr.Zero)
                    {
                        IntPtr ptr = GlobalLock(handle);
                        try
                        {
                            unsafe
                            {
                                byte* pDib = (byte*)ptr.ToPointer();
                                int headerSize = Marshal.ReadInt32((IntPtr)pDib);
                                int width = Marshal.ReadInt32((IntPtr)(pDib + 4));
                                int height = Marshal.ReadInt32((IntPtr)(pDib + 8));
                                int bitCount = Marshal.ReadInt16((IntPtr)(pDib + 14));
                                int stride = ((width * bitCount + 31) / 32) * 4;
                                int colorTableSize = (bitCount <= 8) ? ((1 << bitCount) * 4) : 0;
                                byte* pBits = pDib + headerSize + colorTableSize;

                                PixelFormat pixelFormat = bitCount == 24 ? PixelFormat.Format24bppRgb :
                                                         bitCount == 32 ? PixelFormat.Format32bppRgb :
                                                         bitCount == 8 ? PixelFormat.Format8bppIndexed :
                                                         PixelFormat.Format1bppIndexed;

                                int absHeight = Math.Abs(height);
                                var bmp = new Bitmap(width, absHeight, pixelFormat);
                                var bmpData = bmp.LockBits(new Rectangle(0, 0, width, absHeight), ImageLockMode.WriteOnly, pixelFormat);

                                try
                                {
                                    if (height > 0)
                                    {
                                        for (int row = 0; row < absHeight; row++)
                                        {
                                            byte* srcRow = pBits + (absHeight - 1 - row) * stride;
                                            byte* dstRow = (byte*)bmpData.Scan0 + row * bmpData.Stride;
                                            Buffer.MemoryCopy(srcRow, dstRow, bmpData.Stride, stride);
                                        }
                                    }
                                    else
                                    {
                                        Buffer.MemoryCopy(pBits, (void*)bmpData.Scan0, stride * absHeight, stride * absHeight);
                                    }
                                }
                                finally
                                {
                                    bmp.UnlockBits(bmpData);
                                }

                                Directory.CreateDirectory(outputPath);
                                string fileName = $"scan_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                                string fullPath = Path.Combine(outputPath, fileName);

                                bmp.Save(fullPath, ImageFormat.Jpeg);
                                result.Success = true;
                                result.ImagePath = fullPath;
                                result.FileName = fileName;
                                bmp.Dispose();
                                dataReceived = true;
                                Console.WriteLine($"? Saved: {fileName}");
                            }
                        }
                        finally
                        {
                            GlobalUnlock(handle);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"? Error: {ex.Message}");
                    scanException = ex;
                    result.Success = false;
                    result.ErrorMessage = ex.Message;
                }
            };

            EventHandler<TransferErrorEventArgs> transferErrorHandler = (s, e) =>
            {
                Console.WriteLine($"? Transfer Error: {e.ReturnCode}");
                scanException = e.Exception ?? new Exception(e.ReturnCode.ToString());
                result.Success = false;
                result.ErrorMessage = scanException.Message;
                scanComplete = true;
            };

            EventHandler sourceDisabledHandler = (s, e) =>
            {
                Console.WriteLine("? Source Disabled");
                scanComplete = true;
            };

            try
            {
                var sources = _session.ToList();
                src = sources.FirstOrDefault(s => s.Name == scannerId);

                if (src == null)
                    throw new Exception($"Scanner '{scannerId}' not found");

                // Subscribe to session events (these fire on the STA thread)
                _session.DataTransferred += dataTransferredHandler;
                _session.TransferError += transferErrorHandler;
                _session.SourceDisabled += sourceDisabledHandler;

                Console.WriteLine("? Opening scanner...");
                var openResult = src.Open();
                if (openResult != ReturnCode.Success)
                    throw new Exception($"Failed to open scanner: {openResult}");
                
                sourceOpened = true;

                Console.WriteLine("? Enabling scanner (NoUI, auto-scan)...");
                // NoUI mode with modal=false allows automatic scanning
                var enableResult = src.Enable(SourceEnableMode.NoUI, false, _messageLoopForm.Handle);
                if (enableResult != ReturnCode.Success)
                    throw new Exception($"Failed to enable scanner: {enableResult}");
                
                Console.WriteLine("? Scanner enabled");
                
                // Give scanner a moment to detect document and initialize
                Console.WriteLine("? Checking for document...");
                for (int i = 0; i < 20; i++) // 2 seconds to detect
                {
                    Application.DoEvents();
                    Thread.Sleep(100);
                }
                
                Console.WriteLine("? Ready, waiting for scan...");

                // Wait and pump messages on THIS thread (the STA thread where events will fire)
                var startTime = DateTime.Now;
                var lastLog = DateTime.Now;
                
                while (!scanComplete && (DateTime.Now - startTime).TotalSeconds < 20)
                {
                    // CRITICAL: Process messages on the STA thread
                    Application.DoEvents();
                    Thread.Sleep(100);
                    
                    if ((DateTime.Now - lastLog).TotalSeconds >= 3 && !dataReceived)
                    {
                        Console.WriteLine($"? Waiting ({(int)(DateTime.Now - startTime).TotalSeconds}s)...");
                        lastLog = DateTime.Now;
                    }
                    
                    if (dataReceived && !scanComplete)
                    {
                        var wait = DateTime.Now;
                        while ((DateTime.Now - wait).TotalMilliseconds < 1500 && !scanComplete)
                        {
                            Application.DoEvents();
                            Thread.Sleep(50);
                        }
                        if (!scanComplete)
                            scanComplete = true;
                    }
                }

                if (scanException != null)
                    throw scanException;

                if (!dataReceived)
                    throw new TimeoutException("No document detected. Place document in feeder and ensure it's detected by scanner.");

                Console.WriteLine("? Scan complete\n");
                return result;
            }
            finally
            {
                try
                {
                    if (_session != null)
                    {
                        _session.DataTransferred -= dataTransferredHandler;
                        _session.TransferError -= transferErrorHandler;
                        _session.SourceDisabled -= sourceDisabledHandler;
                    }
                }
                catch { }

                if (src != null && sourceOpened)
                {
                    try 
                    { 
                        Console.WriteLine("? Closing scanner...");
                        src.Close();
                        Console.WriteLine("? Scanner closed");
                        
                        // CRITICAL: Give scanner time to fully reset
                        Thread.Sleep(1000);
                        
                        // Extra message pumping to ensure cleanup
                        for (int i = 0; i < 10; i++)
                        {
                            Application.DoEvents();
                            Thread.Sleep(50);
                        }
                    } 
                    catch (Exception ex)
                    {
                        Console.WriteLine($"? Close error: {ex.Message}");
                    }
                }
                
                Console.WriteLine("? Ready for next scan\n");
            }
        }

        private static ScanResult PerformSimpleScanWithAutoTrigger(string scannerId, string outputPath)
        {
            var result = new ScanResult();

            try
            {
                Directory.CreateDirectory(outputPath);

                var scanLocations = new List<string>
                {
                    outputPath,
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Scanned Documents"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Scans"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Epson"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Scanned Documents"),
                    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments), "Scanned Documents"),
                    @"C:\Users\Public\Documents\Scanned Documents",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop))
                };

                var baselineFiles = new Dictionary<string, HashSet<string>>();
                
                Console.WriteLine("?? Monitoring folders:");
                foreach (var location in scanLocations.Where(Directory.Exists))
                {
                    var existingFiles = new HashSet<string>(
                        Directory.GetFiles(location, "*.*", SearchOption.TopDirectoryOnly)
                        .Where(f => {
                            var ext = Path.GetExtension(f).ToLower();
                            return ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp" || ext == ".pdf";
                        })
                    );
                    baselineFiles[location] = existingFiles;
                }

                // TRIGGER SCAN AUTOMATICALLY using Epson Scan utility
                Console.WriteLine($"\n? AUTO-TRIGGERING scan on {scannerId}...");
                
                // Try to launch Epson Scan with auto-scan parameter
                try
                {
                    var epsonPaths = new[]
                    {
                        @"C:\Program Files (x86)\epson\Epson Scan 2\Core\es2launcher.exe",
                        @"C:\Program Files\epson\Epson Scan 2\Core\es2launcher.exe",
                        @"C:\Windows\twain_32\escndv\escndv.exe",
                        @"C:\Windows\twain_32\epson\escndv\escndv.exe"
                    };

                    string epsonExe = epsonPaths.FirstOrDefault(File.Exists);
                    
                    if (epsonExe != null)
                    {
                        Console.WriteLine($"? Found Epson Scan: {epsonExe}");
                        
                        // Start Epson Scan with parameters for auto-scan
                        var startInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = epsonExe,
                            Arguments = "/AUTO",  // Auto-scan parameter
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                        };
                        
                        System.Diagnostics.Process.Start(startInfo);
                        Console.WriteLine("? Scan triggered via Epson utility");
                    }
                    else
                    {
                        Console.WriteLine("? Epson Scan utility not found, waiting for manual scan...");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"? Could not auto-trigger: {ex.Message}");
                    Console.WriteLine("  Waiting for manual scan button press...");
                }

                Console.WriteLine("?? Monitoring for scanned file...\n");

                // Monitor folders for new files
                var startTime = DateTime.Now;
                var timeout = TimeSpan.FromSeconds(30); // Reduced timeout since auto-triggered
                string newFile = null;
                string foundLocation = null;

                while ((DateTime.Now - startTime) < timeout)
                {
                    Thread.Sleep(500);

                    foreach (var kvp in baselineFiles)
                    {
                        var location = kvp.Key;
                        var baseline = kvp.Value;

                        if (!Directory.Exists(location)) continue;

                        var currentFiles = Directory.GetFiles(location, "*.*", SearchOption.TopDirectoryOnly)
                            .Where(f => {
                                var ext = Path.GetExtension(f).ToLower();
                                return ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp" || ext == ".pdf";
                            })
                            .Where(f => !baseline.Contains(f))
                            .OrderByDescending(f => File.GetCreationTime(f))
                            .ToList();

                        if (currentFiles.Any())
                        {
                            newFile = currentFiles.First();
                            foundLocation = location;
                            Console.WriteLine($"? New file detected: {Path.GetFileName(newFile)}");
                            Console.WriteLine($"? Location: {location}");
                            break;
                        }
                    }

                    if (newFile != null) break;

                    var elapsed = (int)(DateTime.Now - startTime).TotalSeconds;
                    if (elapsed > 0 && elapsed % 5 == 0)
                    {
                        Console.WriteLine($"? Waiting... ({elapsed}s / 30s)");
                    }
                }

                if (newFile == null)
                {
                    throw new TimeoutException("No scan detected. Ensure document is loaded in scanner feeder.");
                }

                // Wait for file to be fully written
                Console.WriteLine($"? Waiting for file to be fully written...");
                Thread.Sleep(3000); // Increased from 2000 to 3000ms

                // Retry mechanism - check multiple times for valid files
                string validFile = null;
                var maxRetries = 10; // Try 10 times
                var retryDelay = 500; // 500ms between retries

                for (int retry = 0; retry < maxRetries; retry++)
                {
                    // Get ALL new files in the location
                    var allNewFiles = Directory.GetFiles(foundLocation, "*.*", SearchOption.TopDirectoryOnly)
                        .Where(f => {
                            var ext = Path.GetExtension(f).ToLower();
                            return ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp" || ext == ".pdf";
                        })
                        .Where(f => !baselineFiles[foundLocation].Contains(f)) // Files NOT in baseline
                        .Select(f => new FileInfo(f))
                        .Where(fi => fi.Length > 0) // Has content
                        .OrderByDescending(fi => fi.CreationTime)
                        .ToList();

                    if (allNewFiles.Any())
                    {
                        validFile = allNewFiles.First().FullName;
                        Console.WriteLine($"? Found valid file: {Path.GetFileName(validFile)} ({allNewFiles.First().Length} bytes) on retry {retry + 1}");
                        break;
                    }

                    if (retry < maxRetries - 1)
                    {
                        Console.WriteLine($"? Retry {retry + 1}/{maxRetries} - waiting for scanner to finish writing...");
                        Thread.Sleep(retryDelay);
                    }
                }

                if (validFile == null)
                {
                    Console.WriteLine("? No valid scanned file found after all retries!");
                    Console.WriteLine("   This might be a timing issue with the scanner.");
                    throw new Exception("No valid scanned file found. Scanner may still be writing files.");
                }

                newFile = validFile;
                var fileInfo = new FileInfo(newFile);
                Console.WriteLine($"? File ready: {Path.GetFileName(newFile)} ({fileInfo.Length} bytes)");

                // Use the original file directly instead of moving/copying
                result.Success = true;
                result.ImagePath = newFile;
                result.FileName = Path.GetFileName(newFile);

                Console.WriteLine($"? SUCCESS: {result.FileName}");
                Console.WriteLine($"? Location: {newFile}");

                // PERFORM OCR
                Console.WriteLine("\n?? Performing OCR on scanned image...");
                try
                {
                    result.OCRData = OCRService.PerformMultiLanguageOCR(newFile); // Use the original file
                    
                    if (result.OCRData.Success)
                    {
                        Console.WriteLine($"? OCR completed successfully");
                        Console.WriteLine($"   Extracted {result.OCRData.ExtractedText?.Length ?? 0} characters");
                    }
                    else
                    {
                        Console.WriteLine($"? OCR failed: {result.OCRData.ErrorMessage}");
                    }
                }
                catch (Exception ocrEx)
                {
                    Console.WriteLine($"? OCR error: {ocrEx.Message}");
                    result.OCRData = new OCRResult
                    {
                        Success = false,
                        ErrorMessage = ocrEx.Message
                    };
                }

                Console.WriteLine();
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Error: {ex.Message}");
                throw;
            }
        }

        [DllImport("kernel32.dll")]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GlobalSize(IntPtr hMem);
    }

    public class ScannerInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Manufacturer { get; set; }
        public string ProductName { get; set; }
    }

    public class ScanResult
    {
        public bool Success { get; set; }
        public string ImagePath { get; set; }
        public string FileName { get; set; }
        public string ErrorMessage { get; set; }
        public OCRResult OCRData { get; set; }
    }
}
