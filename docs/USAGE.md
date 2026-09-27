# Usage Guide

Step-by-step usage of **Jhoinrch CAN UPdate Tool**.  
Insert one screenshot under each step, then upload this file to GitHub.

---

## 1. Enter DFU mode

Put the device into DFU mode (usually hold **BOOT0** while powering on).

<!-- INSERT PHOTO: device / BOOT0 enter DFU -->
![Step 1 — Enter DFU mode](docs/images/step-01-enter-dfu.png)

---

## 2. Install WinUSB driver (if needed)

If the device is not detected, click **ZADIG**, pick `DFU in FS Mode` → **WinUSB** → **Replace Driver**.

<!-- INSERT PHOTO: Zadig driver replace dialog -->
![Step 2 — Zadig WinUSB driver](docs/images/step-02-zadig-driver.png)

---

## 3. Refresh and select device

Click **Refresh** to enumerate DFU devices, then select the target device in the list.

<!-- INSERT PHOTO: device combo / refresh -->
![Step 3 — Refresh and select device](docs/images/step-03-select-device.png)

---

## 4. Select and parse firmware

Click **Browse** to choose a `.dfu` or `.bin` file, then click **Parse** to inspect format, address, size, VID:PID, and CRC.

<!-- INSERT PHOTO: browse + parse result -->
![Step 4 — Select and parse firmware](docs/images/step-04-parse-firmware.png)

---

## 5. Set address (for `.bin` only)

For `.bin` files, fill in the flash address (default `0x08000000`).  
`.dfu` files already contain the address, so this box is disabled.

<!-- INSERT PHOTO: address / length fields -->
![Step 5 — Set flash address](docs/images/step-05-set-address.png)

---

## 6. Flash or Read

- **Flash** — download firmware to the device, then it reboots into the new firmware
- **Read** — export firmware from the device to a `.bin` file
- **Cancel** — stop the running operation

Watch the progress bar and the log panel at the bottom.

<!-- INSERT PHOTO: flash / read buttons + progress -->
![Step 6 — Flash or Read](docs/images/step-06-flash-or-read.png)

---

## Tips

- If enumeration fails, re-check BOOT0 and the WinUSB driver.
- Prefer `.dfu` when available — it carries address and CRC info.
- Keep the USB cable stable during flash; do not unplug mid-write.
