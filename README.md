# ??? Scanner Web API - Complete Solution

## ?? Overview
A complete REST API solution that allows web applications (Angular, React, Vue, etc.) to communicate with TWAIN-compatible scanners. Perfect for document scanning workflows with OCR and form filling.

## ? Features
- ? List all connected TWAIN scanners
- ? Scan documents programmatically
- ? Return scanned images via HTTP
- ? CORS enabled for web applications
- ? Automatic image timestamping
- ? RESTful API design
- ? Full Angular integration examples

## ??? Architecture

```
???????????????         ????????????????         ??????????????
?   Angular   ?  HTTP   ?   .NET API   ?  TWAIN  ?  Scanner   ?
?     App     ? ??????? ?   Service    ? ??????? ?  Hardware  ?
???????????????         ????????????????         ??????????????
      ?                        ?
      ?                        ?
      ?                  ??????????????
      ????????????????????   Images   ?
            Receives URL ?   Folder   ?
                         ??????????????
```

## ?? Prerequisites
- Windows Operating System
- .NET Framework 4.8
- TWAIN-compatible scanner with drivers installed
- Administrator privileges (for HTTP.sys)

## ?? Quick Start

### 1. Build and Run the API
```bash
# Build the project
dotnet build

# Run as Administrator
# Right-click ConsoleApp2.exe ? Run as Administrator
```

### 2. Verify API is Running
You should see:
```
==============================================
       Scanner Web API Service
==============================================

Initializing scanner service...
Scanner service initialized successfully.

Scanner Web API started on http://localhost:5195/
Scan images will be saved to: C:\Users\...\Documents\Scans

Available endpoints:
  GET  http://localhost:5195/api/scanners
  POST http://localhost:5195/api/scan
  GET  http://localhost:5195/api/images/{filename}
```

### 3. Test the API

#### List Scanners
```bash
curl http://localhost:5195/api/scanners
```

#### Scan Document
```bash
curl -X POST http://localhost:5195/api/scan \
  -H "Content-Type: application/json" \
  -d "{\"scannerId\":\"EPSON DS-510\"}"
```

## ?? Project Structure
```
ConsoleApp2/
?
??? Program.cs              # Main entry point, starts Web API
??? ScannerService.cs       # Core scanning logic with TWAIN
??? ScannerWebAPI.cs        # HTTP API implementation
??? API_DOCUMENTATION.md    # Complete API reference
??? README.md              # This file
```

## ?? API Endpoints

### 1. GET /api/scanners
Lists all connected scanners.

**Response:**
```json
{
  "success": true,
  "scanners": [
    {
      "id": "EPSON DS-510",
      "name": "EPSON DS-510",
      "manufacturer": "EPSON",
      "productName": "DS-510"
    }
  ]
}
```

### 2. POST /api/scan
Scans a document.

**Request:**
```json
{
  "scannerId": "EPSON DS-510"
}
```

**Response:**
```json
{
  "success": true,
  "imagePath": "C:\\Users\\...\\scan_20250101_143022.jpg",
  "fileName": "scan_20250101_143022.jpg",
  "imageUrl": "http://localhost:5195/api/images/scan_20250101_143022.jpg"
}
```

### 3. GET /api/images/{filename}
Downloads a scanned image.

## ??? Angular Integration

### Install Service
```typescript
// scanner.service.ts
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';

@Injectable({ providedIn: 'root' })
export class ScannerService {
  private apiUrl = 'http://localhost:5195/api';

  constructor(private http: HttpClient) { }

  getScanners() {
    return this.http.get(`${this.apiUrl}/scanners`);
  }

  scan(scannerId: string) {
    return this.http.post(`${this.apiUrl}/scan`, { scannerId });
  }
}
```

### Use in Component
```typescript
// In your component
constructor(private scannerService: ScannerService) { }

startScan() {
  this.scannerService.scan('EPSON DS-510').subscribe(
    (result: any) => {
      if (result.success) {
        this.imageUrl = result.imageUrl;
        // Now process with OCR, fill forms, etc.
      }
    }
  );
}
```

See `API_DOCUMENTATION.md` for complete Angular examples.

## ?? Configuration

### Change API Port
Edit `Program.cs`:
```csharp
var api = new ScannerWebAPI("http://localhost:8080/");
```

### Change Save Location
Edit `Program.cs`:
```csharp
var customPath = @"C:\MyScans";
var api = new ScannerWebAPI("http://localhost:5195/", customPath);
```

## ?? Troubleshooting

### Issue: "Access Denied" when starting
**Solution:** Run as Administrator (required for HTTP.sys)

### Issue: "Scanner not found"
**Solutions:**
1. Ensure scanner is powered on and connected
2. Install latest TWAIN drivers from manufacturer
3. Test scanner in Windows "Devices and Printers"

### Issue: CORS errors in browser
**Solution:** API already includes CORS headers. Verify API is running and accessible.

### Issue: "DLL not found" (twaindsm.dll)
**Solution:** Install TWAIN Data Source Manager from http://www.twain.org/

## ?? Complete Workflow Example

```
1. User opens Angular app
   ?
2. Angular calls GET /api/scanners
   ?
3. User selects "EPSON DS-510" from dropdown
   ?
4. User clicks "Scan" button
   ?
5. Angular calls POST /api/scan with scannerId
   ?
6. .NET API ? TWAIN ? Scanner hardware
   ?
7. Document is scanned
   ?
8. Image saved to Documents\Scans\scan_20250101_143022.jpg
   ?
9. API returns image URL
   ?
10. Angular displays image
   ?
11. Angular sends image to OCR service
   ?
12. OCR returns text
   ?
13. Angular fills form fields automatically
```

## ?? Key Technologies
- **NTwain**: .NET TWAIN library for scanner communication
- **HttpListener**: Self-hosted HTTP server
- **Windows Forms**: Provides message loop for TWAIN
- **DIB Parsing**: Converts TWAIN native format to JPEG

## ?? Dependencies
```xml
<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
<PackageReference Include="NTwain" Version="3.6.1" />
<PackageReference Include="System.Drawing.Common" Version="10.0.1" />
```

## ?? Contributing
Feel free to submit issues and enhancement requests!

## ?? License
[Your License Here]

## ?? Success!
You now have a fully functional scanner Web API that Angular can communicate with!

**Next Steps:**
1. Run the API as Administrator
2. Integrate with your Angular app using the examples
3. Add OCR processing
4. Implement form auto-fill logic

Happy Scanning! ??
