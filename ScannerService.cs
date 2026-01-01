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

                _staThread = new Thread(() =>
                {
                    _messageLoopForm = new Form();
                    _messageLoopForm.ShowInTaskbar = false;
                    _messageLoopForm.WindowState = FormWindowState.Minimized;
                    _messageLoopForm.Opacity = 0;
                    _messageLoopForm.CreateControl();

                    _syncContext = SynchronizationContext.Current;

                    var appId = TWIdentity.CreateFromAssembly(DataGroups.Image, System.Reflection.Assembly.GetExecutingAssembly());
                    _session = new TwainSession(appId);
                    _session.Open();

                    _isInitialized = true;

                    Application.Run(_messageLoopForm);
                });

                _staThread.SetApartmentState(ApartmentState.STA);
                _staThread.IsBackground = true;
                _staThread.Start();

                var timeout = DateTime.Now.AddSeconds(5);
                while (!_isInitialized && DateTime.Now < timeout)
                {
                    Thread.Sleep(100);
                }

                if (!_isInitialized)
                {
                    throw new Exception("Failed to initialize scanner service");
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

                _session.DataTransferred += dataTransferredHandler;
                _session.TransferError += transferErrorHandler;
                _session.SourceDisabled += sourceDisabledHandler;

                Console.WriteLine("? Opening scanner...");
                var openResult = src.Open();
                if (openResult != ReturnCode.Success)
                    throw new Exception($"Failed to open scanner: {openResult}");
                
                sourceOpened = true;
                Console.WriteLine("? Scanner opened");

                Console.WriteLine("? Enabling scanner (ShowUI mode - will show scanner dialog)...");
                // Use ShowUI mode - this is more reliable and avoids threading issues
                var enableResult = src.Enable(SourceEnableMode.ShowUI, true, _messageLoopForm.Handle);
                if (enableResult != ReturnCode.Success)
                    throw new Exception($"Failed to enable scanner: {enableResult}");
                
                Console.WriteLine("? Scanner dialog shown, waiting for user to scan...");

                // Wait longer since user needs to interact with dialog
                var startTime = DateTime.Now;
                var lastLogTime = DateTime.Now;
                
                while (!scanComplete && (DateTime.Now - startTime).TotalSeconds < 60)
                {
                    Application.DoEvents();
                    Thread.Sleep(50);
                    
                    if ((DateTime.Now - lastLogTime).TotalSeconds >= 5 && !dataReceived)
                    {
                        Console.WriteLine($"? Waiting for scan... ({(int)(DateTime.Now - startTime).TotalSeconds}s)");
                        lastLogTime = DateTime.Now;
                    }
                    
                    if (dataReceived && !scanComplete)
                    {
                        var waitStart = DateTime.Now;
                        while ((DateTime.Now - waitStart).TotalMilliseconds < 2000 && !scanComplete)
                        {
                            Application.DoEvents();
                            Thread.Sleep(50);
                        }
                        if (!scanComplete)
                        {
                            Console.WriteLine("? Completing");
                            scanComplete = true;
                        }
                    }
                }

                if (scanException != null)
                    throw scanException;

                if (!dataReceived)
                {
                    Console.WriteLine("? Timeout or cancelled");
                    throw new TimeoutException("Scan was cancelled or timed out.");
                }

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
                    try { src.Close(); } catch { }
                }
                
                Thread.Sleep(500);
                Application.DoEvents();
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
    }
}
