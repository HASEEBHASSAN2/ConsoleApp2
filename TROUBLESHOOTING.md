# Scanner Troubleshooting Guide

## Issue: Scan timeouts after 3rd request

### What's Happening:
The scanner enables successfully but the `DataTransferred` event never fires, meaning TWAIN isn't receiving data from the scanner.

### Possible Causes:

1. **Document Not Loaded**
   - The Epson DS-510 requires a document to be in the feeder
   - With `SourceEnableMode.NoUI`, the scanner waits for a document
   - Solution: **Load a document before clicking "Scan"**

2. **Scanner State Issue**
   - After 2 successful scans, the scanner might be in a locked state
   - Solution: Power cycle the scanner or try unplugging/replugging USB

3. **TWAIN Driver Issue**
   - The driver might need to be reset
   - Solution: Close and restart the application

### Testing Steps:

#### Test 1: Document Detection
1. **Before** clicking scan in your app
2. Place a document in the scanner feeder
3. Make sure the scanner detects it (usually an LED indicator)
4. **Then** click "Scan" in your Angular app

#### Test 2: Scanner Reset
If scans still timeout:
1. Stop the application
2. Unplug the scanner USB cable
3. Wait 5 seconds
4. Plug back in
5. Wait for Windows to recognize it
6. Start the application again

#### Test 3: Check Scanner Status
```powershell
# Check if scanner is recognized
Get-PnpDevice | Where-Object {$_.FriendlyName -like "*DS-510*"}
```

### Expected Behavior:

**Working Scan:**
```
DEBUG: Opening scanner source: EPSON DS-510
DEBUG: Enabling scanner...
DEBUG: Scanner enabled successfully
DEBUG: Please ensure document is in the scanner feeder
DEBUG: Waiting for scan to start automatically...
DEBUG: DataTransferred event fired        ? This should appear!
DEBUG: Image saved successfully: scan_xxx.jpg
DEBUG: Data received but SourceDisabled not fired, completing scan anyway
DEBUG: Scan completed successfully
```

**Timeout (Current Issue):**
```
DEBUG: Opening scanner source: EPSON DS-510
DEBUG: Enabling scanner...
DEBUG: Scanner enabled successfully
DEBUG: Waiting for scan to start automatically...
DEBUG: Still waiting... dataReceived=False, scanComplete=False
DEBUG: Still waiting... dataReceived=False, scanComplete=False
...
DEBUG: Scan operation timed out           ? DataTransferred never fired
```

### Quick Fixes to Try:

1. **Load Document First**
   ```
   Most Important: Place document in scanner BEFORE clicking scan button
   ```

2. **Restart Application**
   ```
   Close the Scanner API and restart it
   ```

3. **Check Scanner Settings**
   - Open "Windows Fax and Scan"
   - Test scan from there
   - If it works there but not in our app, it's a TWAIN configuration issue

4. **Power Cycle**
   ```
   Unplug ? Wait ? Replug ? Test
   ```

### For Epson DS-510 Specifically:

The DS-510 is a document feeder scanner. It **requires**:
- ? Document loaded in feeder tray
- ? Scanner powered on
- ? Document detected (green light usually indicates ready)

### Alternative Approach:

If the issue persists, we might need to:
1. Use `SourceEnableMode.ShowUI` instead of `NoUI` (shows scanner dialog)
2. Add manual trigger capability
3. Implement scanner state checking before enabling

### Debug Your Next Scan:

1. **Stop current application**
2. **Load a document in the scanner NOW**
3. **Start the application**
4. **Immediately click Scan** (within 5 seconds)
5. **Watch the console** for:
   - Does `DataTransferred event fired` appear?
   - If YES ? problem is timing/document detection
   - If NO ? problem is scanner state/driver

Let me know what you see! ????
