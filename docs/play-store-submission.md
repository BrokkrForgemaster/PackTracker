# Google Play Store Submission Guide

## 1. Store Listing Metadata

### App Title (Max 30 chars)
**PackTracker**

### Short Description (Max 80 chars)
**Coordinate Star Citizen operations, logistics, and crafting for your pack.**

### Full Description (Max 4000 chars)
PackTracker is the ultimate coordination platform designed for Star Citizen organizations. Built by pilots for pilots, it moves your logistics beyond spreadsheets and into a real-time, high-fidelity command center.

Whether you're running a massive industrial operation or a small PMC, PackTracker gives your members the tools they need to stay synchronized.

**KEY FEATURES:**

* **Operations Dashboard:** A real-time overview of current guild activity, announcements, and mission status.
* **Request Management:** Streamlined workflows for assistance, procurement, and crafting requests with live status updates.
* **Blueprint Explorer:** Browse and manage your organization's blueprint library, calculate materials for massive build queues, and track ownership.
* **Crafting & Procurement Queues:** Coordinate resources across multiple members and ensure your industrial projects stay on track.
* **Trading Data (UEX Integration):** Access high-fidelity trading data to maximize your organization's profit loops.
* **Discord Integration:** Seamlessly bridge your in-game coordination with your Discord server.

**BUILT FOR THE PACK:**
PackTracker is built using .NET MAUI to provide a native experience on Android, while sharing the same robust backend used by the PackTracker desktop platform.

*Note: PackTracker is an unofficial fan project and is not affiliated with Cloud Imperium Games or Robert Space Industries.*

---

## 2. Privacy Policy

**Privacy Policy for PackTracker**
*Last Updated: May 9, 2026*

**1. Data Collection**
PackTracker connects to your organization's self-hosted or managed PackTracker API.
* **Personal Data:** We collect your Discord User ID and username solely for authentication and identity within your organization.
* **Usage Data:** We do not track your location or sell your data to third parties.

**2. Data Storage**
All operational data (requests, blueprints, etc.) is stored in the PostgreSQL database managed by your organization. Authentication tokens are stored securely on your device using Android Secure Storage.

**3. Permissions**
* **Internet:** Required to communicate with your PackTracker API.
* **Notifications:** Used to alert you of status changes to your requests (if enabled).

**4. Contact**
For data deletion requests or privacy concerns, contact your organization's PackTracker administrator.

---

## 3. Visual Asset Manifest

| Asset | Requirement | Source/Action |
| --- | --- | --- |
| **App Icon** | 512x512 PNG | Export `PackTracker.Mobile/Resources/AppIcon/appicon.svg` |
| **Feature Graphic** | 1024x500 PNG | Use `docs/images/HousewolfBanner.png` (Crop/Resize) |
| **Screenshots** | Phone/Tablet | Capture from a running Android emulator/device |

---

## 4. Submission Checklist (Human Action Required)

- [ ] **Developer Account**: Pay the $25 fee at [Play Console](https://play.google.com/apps/publish).
- [ ] **Release Keystore**: Generate `packtracker.keystore` (DO NOT COMMIT).
- [ ] **Build AAB**: Run `dotnet publish -c Release`.
- [ ] **Upload**: Submit to the Production track in Play Console.
