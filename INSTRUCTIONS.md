# 🛠️ Hikvision Device Configuration Guide

This guide provides the steps to configure your Hikvision device to send events to the Telegram bot.

## Prerequisites

*   A Hikvision device (camera or NVR) that supports ISAPI.
*   A tool for making API requests, such as [Postman](https://www.postman.com/) or `curl`.
*   The IP address of the machine where the bot is running.

---

## Step 1: Verify Device Capabilities

First, check if your device supports sending event notifications over HTTP.

*   **Action**: Send a `GET` request to the following endpoint on your device:
    ```
    /ISAPI/Event/notification/httpHosts/capabilities
    ```
*   **Expected Result**: A successful response (e.g., `200 OK`) confirming the device's capabilities.

---

## Step 2: Set the Notification Host

Next, you need to tell the Hikvision device where to send the event notifications. This is done by setting up an "HTTP Host" with the IP address and port of the machine where this bot is running.

*   **Action**: Send a `PUT` request to the following endpoint:
    ```
    /ISAPI/Event/notification/httpHosts
    ```
*   **Request Body**: The body of the request must be an XML payload. Replace `YOUR_SERVER_IP` and `YOUR_SERVER_PORT` with the IP address and port of your bot.

    ```xml
    <HttpHostList version="2.0" xmlns="http://www.hikvision.com/ver20/XMLSchema">
      <HttpHost>
        <id>1</id>
        <ipAddress>YOUR_SERVER_IP</ipAddress>
        <portNo>YOUR_SERVER_PORT</portNo>
        <protocol>HTTP</protocol>
        <addressingFormatType>ipaddress</addressingFormatType>
      </HttpHost>
    </HttpHostList>
    ```

*   **`curl` Example**:
    ```bash
    curl -u admin:PASSWORD -X PUT -d \
    '<HttpHostList version="2.0" xmlns="http://www.hikvision.com/ver20/XMLSchema">
      <HttpHost>
        <id>1</id>
        <ipAddress>YOUR_SERVER_IP</ipAddress>
        <portNo>YOUR_SERVER_PORT</portNo>
        <protocol>HTTP</protocol>
        <addressingFormatType>ipaddress</addressingFormatType>
      </HttpHost>
    </HttpHostList>'
    "http://<CAMERA_IP>/ISAPI/Event/notification/httpHosts"
    ```

---

## Step 3: Enable Event Notifications

Setting the notification host is not enough. You also need to enable notifications for each event you want to receive.

*   **Action**:
    1.  Open your device's web interface in a browser.
    2.  Navigate to the settings for the events you are interested in (e.g., Motion Detection, Line Crossing).
    3.  Find the option to "Notify Surveillance Center" or "Notify HTTP Host" and enable it.

---

## Step 4: Test the Connection

After configuring the host, you can ask the camera to send a test notification to verify that it can reach your server.

*   **Action**: Send a `POST` request to the following endpoint:
    ```
    /ISAPI/Event/notification/httpHosts/1/test
    ```
*   **Expected Result**: Your running bot should receive a test event from the device.

---

## Step 5: Run the Bot

Once your device is configured, you can run the bot to start receiving events.

1.  **Install dependencies**:
    ```bash
    pip install -r requirements.txt
    ```
2.  **Run the bot**:
    ```bash
    python main.py
    ```
