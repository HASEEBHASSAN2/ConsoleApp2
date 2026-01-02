# ?? OCR Setup Guide

## ?? Features Added
- ? Tesseract OCR integration
- ? Multi-language support (English + Urdu)
- ? Image preprocessing for better accuracy
- ? Automatic OCR after scanning
- ? OCR results returned in API response

---

## ?? Installation Steps

### 1. Restore NuGet Packages
```bash
dotnet restore
```

### 2. Download Tesseract Language Data

**Required Files:**
- `eng.traineddata` (English) - **REQUIRED**
- `urd.traineddata` (Urdu) - Optional for Urdu text

**Download Links:**
- Fast models (recommended): https://github.com/tesseract-ocr/tessdata_fast
- Best accuracy: https://github.com/tesseract-ocr/tessdata_best

**Where to put them:**
```
C:\Users\HASEEBHASSAN2\source\repos\ConsoleApp2\bin\Debug\net48\tessdata\
  ?? eng.traineddata
  ?? urd.traineddata (optional)
```

### Quick Download Commands:
```powershell
# Create tessdata folder
New-Item -ItemType Directory -Force -Path "bin\Debug\net48\tessdata"

# Download English (Fast)
Invoke-WebRequest -Uri "https://github.com/tesseract-ocr/tessdata_fast/raw/main/eng.traineddata" -OutFile "bin\Debug\net48\tessdata\eng.traineddata"

# Download Urdu (Fast) - Optional
Invoke-WebRequest -Uri "https://github.com/tesseract-ocr/tessdata_fast/raw/main/urd.traineddata" -OutFile "bin\Debug\net48\tessdata\urd.traineddata"
```

---

## ?? API Response (With OCR)

### Request:
```
POST http://localhost:5195/api/scan/simple
Content-Type: application/json

{
  "scannerId": "EPSON DS-510"
}
```

### Response:
```json
{
  "success": true,
  "imagePath": "C:\\Users\\...\\Scans\\scan_20260102_150000.jpg",
  "fileName": "scan_20260102_150000.jpg",
  "imageUrl": "http://localhost:5195/api/images/scan_20260102_150000.jpg",
  "method": "simple_file_monitoring",
  "ocr": {
    "success": true,
    "text": "NATIONAL IDENTITY CARD\nCNIC: 12345-1234567-1\nName: HASEEB HASSAN\nFather Name: ...\n",
    "confidence": 0.94,
    "error": null
  }
}
```

---

## ?? Use Cases

### Pakistani ID Cards:
- ? CNIC (National Identity Card)
- ? Smart Card
- ? B-Form
- ? Driving License

### Medical Cards:
- ? Health Insurance Cards
- ? Patient ID Cards
- ? Prescription slips

### Business Cards:
- ? Name extraction
- ? Phone numbers
- ? Email addresses

---

## ?? OCR Accuracy Tips

### For Best Results:
1. **Good Lighting** - Scanner should have proper illumination
2. **Clean Cards** - Remove dirt/scratches
3. **Flat Placement** - Card should be flat on scanner
4. **High Resolution** - Scanner set to at least 300 DPI

### Preprocessing Applied:
- ? Grayscale conversion
- ? Contrast enhancement
- ? Noise reduction
- ? Binarization (black & white)

---

## ?? OCR Confidence Levels

```
90-100%: Excellent - Text extracted accurately
70-89%:  Good - Minor errors possible
50-69%:  Fair - Manual verification recommended
<50%:    Poor - Image quality issue
```

---

## ?? Testing OCR

### Test Command (Postman):
```
POST http://localhost:5195/api/scan/simple
Body: {"scannerId": "EPSON DS-510"}
```

### Expected Console Output:
```
?? Monitoring folders:
? AUTO-TRIGGERING scan on EPSON DS-510...
? New file detected: img20260102_150000.jpg
? SUCCESS: scan_20260102_150000.jpg

?? Performing OCR on scanned image...
   Language: eng+urd
   ? Image preprocessed
? OCR Complete:
   Confidence: 94%
   Text length: 245 characters
   Preview: NATIONAL IDENTITY CARD...
```

---

## ?? Troubleshooting

### Error: "Tessdata folder not found"
**Solution:** Download language files to `bin\Debug\net48\tessdata\`

### Error: "Language file not found"
**Solution:** Download `eng.traineddata` from GitHub

### Low OCR Accuracy:
1. Check scanner DPI settings (increase to 300+)
2. Clean the scanner glass
3. Ensure good lighting
4. Try `tessdata_best` instead of `tessdata_fast`

### Urdu Text Not Working:
1. Download `urd.traineddata`
2. Place in `tessdata` folder
3. Restart application

---

## ?? Advanced Configuration

### Custom Language:
```csharp
// In OCRService.cs
result.OCRData = OCRService.PerformOCR(fullPath, "eng"); // English only
result.OCRData = OCRService.PerformOCR(fullPath, "urd"); // Urdu only
result.OCRData = OCRService.PerformOCR(fullPath, "eng+urd"); // Both
```

### Custom Character Whitelist:
```csharp
// For numbers only (like CNIC)
engine.SetVariable("tessedit_char_whitelist", "0123456789-");

// For alphanumeric
engine.SetVariable("tessedit_char_whitelist", "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789");
```

---

## ?? References

- Tesseract GitHub: https://github.com/tesseract-ocr/tesseract
- Language Data: https://github.com/tesseract-ocr/tessdata
- Tesseract.NET: https://github.com/charlesw/tesseract

---

## ? Production Checklist

- [ ] Download `eng.traineddata`
- [ ] Download `urd.traineddata` (if needed)
- [ ] Place in `tessdata` folder
- [ ] Test with sample card
- [ ] Verify OCR accuracy
- [ ] Adjust preprocessing if needed
- [ ] Deploy to production

**OCR is now fully integrated!** ??
