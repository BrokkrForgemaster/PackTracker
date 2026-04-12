# PackTracker v0.1.7 User Guide

Welcome to the **House Wolf Operations Center**. This guide provides a comprehensive overview of how to utilize the primary systems within PackTracker.

<a name="chat-dashboard"></a>
## 💬 1. Operations Dashboard & Chat
The **Operations Dashboard** is your primary landing page for real-time fleet coordination.

### **Chat & Communication**
*   **Channels:** Switch between different operational channels (e.g., Global, Logistics, Fleet) using the channel list on the dashboard.
*   **Direct Messages:** Click on an online user to initiate a private direct message.
*   **Real-time Updates:** The chat system is powered by SignalR, ensuring you receive mission-critical messages without refreshing.
*   **Online Presence:** The sidebar on the right shows which members of House Wolf are currently active.

<a name="trading-hub"></a>
## 📈 2. Trading Hub (UexCorp Integration)
The **Trading Hub** provides real-time market intelligence for Star Citizen commodities.

### **Market Intelligence**
*   **Commodity Selection:** Use the dropdown to select a specific commodity (e.g., Gold, Beryl) to analyze.
*   **Route Analysis:** View buy/sell locations, current prices, and profit margins.
*   **ROI & SCU:** The system calculates the Return on Investment (ROI) and profit per SCU to help you prioritize the most lucrative routes.
*   **Data Freshness:** Click **REFRESH DATA** to pull the latest pricing information from the UexCorp API.

<a name="blueprints"></a>
## 📂 3. Blueprint Explorer
The **Blueprints** section is a centralized database of Star Citizen crafting recipes and item specifications.

### **Navigating Blueprints**
*   **Search & Filter:** Search for specific items or filter by category (e.g., Weapons, Armor, Components).
*   **Detailed View:** Selecting a blueprint shows the required materials, crafting duration, and recipe complexity.
*   **In-Game Availability:** Check if a blueprint is currently available in-game or if it's a future release.
*   **Ownership Tracking:** Mark blueprints you own to help the organization track total crafting capacity.

<a name="crafting-center"></a>
## 🛠️ 4. Crafting Center
The **Crafting Center** manages the production of complex items within House Wolf.

### **Workflow**
1.  **Request Creation:** Initiate a crafting request from the Blueprint Explorer.
2.  **Tracking:** View active crafting jobs, their current status (Open, Accepted, In Progress, Completed), and the assigned crafter.
3.  **Material Coordination:** The view shows exactly which materials are needed and if they are currently available or require procurement.
4.  **Completion:** Once an item is crafted, the status is updated to "Completed," and the requester is notified.

<a name="procurement-center"></a>
## 📦 5. Procurement Center
The **Procurement Center** handles the gathering of raw materials needed for crafting and operations.

### **Material Logistics**
*   **Procurement Requests:** These are often automatically generated from Crafting Requests but can also be created manually.
*   **Status Management:** Track material gathering progress.
*   **Assignments:** Logistics officers can accept procurement tasks to fulfill the organization's needs.
*   **Integration:** Completed procurement tasks automatically update linked crafting requests.

<a name="request-hub"></a>
## 🆘 6. Request Hub (Assistance Hub)
The **Request Hub** is for general assistance beyond crafting and logistics.

### **Types of Requests**
*   **Combat Support:** Request escort or combat reinforcements.
*   **Mining Support:** Request a crew for mining operations.
*   **Medical Support:** Request emergency medical assistance or transport.
*   **General Inquiry:** For miscellaneous operational needs.

### **Using the Hub**
*   **Filtering:** Filter requests by **Kind** and **Status** to find tasks you can assist with.
*   **Creation:** Click **NEW REQUEST** to open a dialog and submit your operational need to the organization.
*   **Real-time Broadcast:** New requests are broadcasted to all online users via SignalR for immediate response.

---

*For further assistance, please reach out to your squad leader or technical officer in the #technical-support Discord channel.*
