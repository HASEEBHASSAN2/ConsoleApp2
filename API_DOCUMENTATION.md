# Scanner Web API Documentation

## Overview
This is a REST API service that enables web applications to communicate with TWAIN-compatible scanners.

## Base URL
```
http://localhost:5195/
```

## Endpoints

### 1. Get Available Scanners
**GET** `/api/scanners`

Returns a list of all TWAIN scanners connected to the system.

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

### 2. Scan Document
**POST** `/api/scan`

Initiates a scan operation with the specified scanner.

**Request Body:**
```json
{
  "scannerId": "EPSON DS-510"
}
```

**Response (Success):**
```json
{
  "success": true,
  "imagePath": "C:\\Users\\Username\\Documents\\Scans\\scan_20250101_143022.jpg",
  "fileName": "scan_20250101_143022.jpg",
  "imageUrl": "http://localhost:5195/api/images/scan_20250101_143022.jpg"
}
```

**Response (Error):**
```json
{
  "success": false,
  "error": "Scanner 'EPSON DS-510' not found"
}
```

### 3. Get Scanned Image
**GET** `/api/images/{filename}`

Retrieves a scanned image file.

**Example:**
```
GET http://localhost:5195/api/images/scan_20250101_143022.jpg
```

**Response:** Image file (image/jpeg)

## Angular Integration Example

### 1. Install HttpClient
Make sure you have `HttpClientModule` imported in your `app.module.ts`:

```typescript
import { HttpClientModule } from '@angular/common/http';

@NgModule({
  imports: [HttpClientModule, ...],
  ...
})
export class AppModule { }
```

### 2. Create Scanner Service

```typescript
// scanner.service.ts
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface Scanner {
  id: string;
  name: string;
  manufacturer: string;
  productName: string;
}

export interface ScanResult {
  success: boolean;
  imagePath?: string;
  fileName?: string;
  imageUrl?: string;
  error?: string;
}

@Injectable({
  providedIn: 'root'
})
export class ScannerService {
  private apiUrl = 'http://localhost:5195/api';

  constructor(private http: HttpClient) { }

  getScanners(): Observable<{ success: boolean; scanners: Scanner[] }> {
    return this.http.get<{ success: boolean; scanners: Scanner[] }>(`${this.apiUrl}/scanners`);
  }

  scan(scannerId: string): Observable<ScanResult> {
    return this.http.post<ScanResult>(`${this.apiUrl}/scan`, { scannerId });
  }

  getImageUrl(fileName: string): string {
    return `${this.apiUrl}/images/${fileName}`;
  }
}
```

### 3. Use in Component

```typescript
// scanner.component.ts
import { Component, OnInit } from '@angular/core';
import { ScannerService, Scanner } from './scanner.service';

@Component({
  selector: 'app-scanner',
  template: `
    <div class="scanner-container">
      <h2>Document Scanner</h2>
      
      <!-- Scanner Selection -->
      <div class="form-group">
        <label>Select Scanner:</label>
        <select [(ngModel)]="selectedScannerId" class="form-control">
          <option value="">-- Select Scanner --</option>
          <option *ngFor="let scanner of scanners" [value]="scanner.id">
            {{scanner.name}}
          </option>
        </select>
      </div>

      <!-- Scan Button -->
      <button 
        (click)="startScan()" 
        [disabled]="!selectedScannerId || isScanning"
        class="btn btn-primary">
        <span *ngIf="!isScanning">Start Scan</span>
        <span *ngIf="isScanning">Scanning...</span>
      </button>

      <!-- Status Message -->
      <div *ngIf="statusMessage" 
           [class]="statusType === 'error' ? 'alert alert-danger' : 'alert alert-success'">
        {{statusMessage}}
      </div>

      <!-- Scanned Image Preview -->
      <div *ngIf="scannedImageUrl" class="image-preview">
        <h3>Scanned Image:</h3>
        <img [src]="scannedImageUrl" alt="Scanned document" style="max-width: 100%;">
        <button (click)="processWithOCR()" class="btn btn-success">
          Process with OCR
        </button>
      </div>
    </div>
  `
})
export class ScannerComponent implements OnInit {
  scanners: Scanner[] = [];
  selectedScannerId: string = '';
  isScanning: boolean = false;
  statusMessage: string = '';
  statusType: 'success' | 'error' = 'success';
  scannedImageUrl: string | null = null;

  constructor(private scannerService: ScannerService) { }

  ngOnInit() {
    this.loadScanners();
  }

  loadScanners() {
    this.scannerService.getScanners().subscribe({
      next: (response) => {
        if (response.success) {
          this.scanners = response.scanners;
          if (this.scanners.length === 1) {
            this.selectedScannerId = this.scanners[0].id;
          }
        }
      },
      error: (error) => {
        this.showError('Failed to load scanners: ' + error.message);
      }
    });
  }

  startScan() {
    if (!this.selectedScannerId) {
      this.showError('Please select a scanner');
      return;
    }

    this.isScanning = true;
    this.statusMessage = 'Scanning in progress...';
    this.statusType = 'success';
    this.scannedImageUrl = null;

    this.scannerService.scan(this.selectedScannerId).subscribe({
      next: (result) => {
        this.isScanning = false;
        if (result.success) {
          this.showSuccess('Scan completed successfully!');
          this.scannedImageUrl = result.imageUrl!;
        } else {
          this.showError('Scan failed: ' + result.error);
        }
      },
      error: (error) => {
        this.isScanning = false;
        this.showError('Scan error: ' + error.message);
      }
    });
  }

  processWithOCR() {
    // Here you can add your OCR processing logic
    console.log('Processing image with OCR:', this.scannedImageUrl);
    // Call your OCR service...
  }

  showSuccess(message: string) {
    this.statusMessage = message;
    this.statusType = 'success';
  }

  showError(message: string) {
    this.statusMessage = message;
    this.statusType = 'error';
  }
}
```

## Setup Instructions

### 1. Run the Scanner API
1. Build the project in Visual Studio
2. Run as Administrator (required for HTTP listener on localhost:5000)
3. The API will start and display available endpoints

### 2. Angular Development
- The API has CORS enabled for all origins (`*`)
- Use the scanner service examples above
- Images are saved to `Documents\Scans` folder by default

## Workflow
```
User Action (Angular) ? HTTP Request ? .NET API
                                          ?
                                    TWAIN Scanner
                                          ?
                                    Image Saved
                                          ?
                                    Return Image URL
                                          ?
Angular Receives URL ? Display/Process ? OCR ? Form Fill
```

## Notes
- The API must run as Administrator for HTTP.sys to bind to port 5000
- Place documents in the scanner before calling the scan endpoint
- The scanner will automatically detect and scan documents
- Images are saved with timestamp: `scan_YYYYMMDD_HHMMSS.jpg`
- Default save location: `%USERPROFILE%\Documents\Scans\`

## Troubleshooting

### "Access Denied" Error
Run the application as Administrator.

### "Scanner not found"
- Ensure the scanner is connected and powered on
- Check Windows Devices and Printers
- Verify TWAIN drivers are installed

### CORS Issues
The API includes CORS headers. If issues persist, check browser console for specific errors.
