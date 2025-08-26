# Hikvision Event Telegram Bot

---

**⚠️ Public Demo Bot Notice ⚠️**

This project is a demonstration and is intended for public showcase purposes. Please be aware of the following:

*   **Security**: Do not use sensitive or production credentials with this bot. It is not intended for use in a production environment.
*   **Stability**: As a demo, this bot may have limitations or bugs. It is provided "as-is" without any guarantees.
*   **Data Handling**: The bot will handle the credentials you provide to connect to the Hikvision device. Be mindful of the data you are using.

---

## Description

This Telegram bot connects to a Hikvision access control device to listen for real-time events. When an event occurs (e.g., an access card is used), the bot will send a notification with details and an image (if available) to your Telegram chat.

## Features

*   Easy setup through a conversational interface.
*   Real-time event notifications from your Hikvision device.
*   Sends event details including IP address, time, name, and event type.
*   Displays a picture associated with the event, if available.

## Setup and Installation

Follow these steps to set up and run the bot on your own system.

### Prerequisites

*   Python 3.7+
*   A Telegram Bot Token. You can get one by talking to the [BotFather](https://t.me/botfather).
*   Access to a Hikvision access control device on your network.

### Installation Steps

1.  **Clone the repository:**
    ```bash
    git clone https://github.com/your-username/hikvision-bot.git
    cd hikvision-bot
    ```

2.  **Create and activate a virtual environment:**
    ```bash
    python -m venv venv
    # On Windows
    venv\Scripts\activate
    # On macOS/Linux
    source venv/bin/activate
    ```

3.  **Install the required dependencies:**
    ```bash
    pip install -r requirements.txt
    ```

4.  **Set up your environment variables:**
    Create a file named `.env` in the root directory of the project and add your Telegram bot token to it:
    ```
    TELEGRAM_BOT_TOKEN="YOUR_TELEGRAM_BOT_TOKEN_HERE"
    ```

## How to Run

Once you have completed the setup, you can run the bot with the following command:

```bash
python main.py
```

The bot will start, and you can begin interacting with it on Telegram.

## How to Use

1.  **Find your bot** on Telegram (the one you created with BotFather).
2.  Send the `/start` command to begin the setup process.
3.  The bot will ask for the following information:
    *   Username for the Hikvision device (default is `admin`).
    *   Password for the Hikvision device.
    *   IP address of the device.
    *   Port number for the device (default is `80`).
4.  Once connected, the bot will notify you that it is listening for events.
5.  To stop the bot, you can use the `/stop` command.

**For more info. visit HikVision for Developers**

## License

This project is licensed under the terms of the `LICENCE` file.
