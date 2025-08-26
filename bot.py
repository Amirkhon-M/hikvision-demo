import asyncio
import logging
import os
import threading

from telegram import ReplyKeyboardRemove, Update
from telegram.ext import (
    Application,
    CommandHandler,
    ContextTypes,
    ConversationHandler,
    MessageHandler,
    filters,
)

from hikvision import HikvisionClient

# Logging
logging.basicConfig(
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s", level=logging.INFO
)
logger = logging.getLogger(__name__)

# States
USERNAME, PASSWORD, DEVICE_IP, PORT, LISTENING = range(5)

# Sessions
user_sessions = {}


async def start(update: Update, context: ContextTypes.DEFAULT_TYPE) -> int:
    # Start conversation.
    await update.message.reply_text(
        "Hi! I'm your Hikvision Event Bot.\n"
        "Let's set up your device. Please send me the username (default: admin)."
    )
    return USERNAME


async def username(update: Update, context: ContextTypes.DEFAULT_TYPE) -> int:
    # Store username.
    context.user_data["username"] = update.message.text
    await update.message.reply_text("Great. Now, please send me the password.")
    return PASSWORD


async def password(update: Update, context: ContextTypes.DEFAULT_TYPE) -> int:
    # Store password.
    context.user_data["password"] = update.message.text
    await update.message.reply_text(
        "Almost there. Please send me the device IP address."
    )
    return DEVICE_IP


async def device_ip(update: Update, context: ContextTypes.DEFAULT_TYPE) -> int:
    # Store device IP.
    context.user_data["device_ip"] = update.message.text
    await update.message.reply_text(
        "Got it. Now, please send me the port number (default: 80)."
    )
    return PORT


async def port(update: Update, context: ContextTypes.DEFAULT_TYPE) -> int:
    # Store port and connect.
    user = update.message.from_user
    context.user_data["port"] = update.message.text

    await update.message.reply_text("Thank you. Trying to connect to the device...")

    try:
        loop = asyncio.get_running_loop()
        client = HikvisionClient(
            host=context.user_data["device_ip"],
            port=context.user_data["port"],
            username=context.user_data["username"],
            password=context.user_data["password"],
            bot=context.bot,
            chat_id=update.effective_chat.id,
            loop=loop,
        )
        user_sessions[user.id] = client

        # Start listening.
        listener_thread = threading.Thread(target=client.start_listening)
        listener_thread.daemon = True
        listener_thread.start()

        await update.message.reply_text(
            "Successfully connected! I am now listening for events.\n"
            "You can use /stop to stop listening."
        )
        return LISTENING
    except Exception as e:
        logger.error(f"Authentication failed for user {user.id}: {e}")
        await update.message.reply_text(
            "Authentication failed. Please check your credentials and try again with /start."
        )
        return ConversationHandler.END


async def stop(update: Update, context: ContextTypes.DEFAULT_TYPE) -> int:
    # Stop listening.
    user = update.message.from_user
    if user.id in user_sessions:
        user_sessions[user.id].stop_listening()
        del user_sessions[user.id]
        await update.message.reply_text(
            "Stopped listening for events. Use /start to connect again."
        )
    else:
        await update.message.reply_text("You are not currently listening for events.")

    return ConversationHandler.END


async def cancel(update: Update, context: ContextTypes.DEFAULT_TYPE) -> int:
    # Cancel conversation.
    user = update.message.from_user
    logger.info("User %s canceled the conversation.", user.first_name)
    await update.message.reply_text(
        "Bye! I hope we can talk again some day.", reply_markup=ReplyKeyboardRemove()
    )

    return ConversationHandler.END


def main() -> None:
    # Run bot.
    application = (
        Application.builder().token(os.getenv("TELEGRAM_BOT_TOKEN")).build()
    )

    conv_handler = ConversationHandler(
        entry_points=[CommandHandler("start", start)],
        states={
            USERNAME: [MessageHandler(filters.TEXT & ~filters.COMMAND, username)],
            PASSWORD: [MessageHandler(filters.TEXT & ~filters.COMMAND, password)],
            DEVICE_IP: [
                MessageHandler(filters.TEXT & ~filters.COMMAND, device_ip)
            ],
            PORT: [MessageHandler(filters.TEXT & ~filters.COMMAND, port)],
            LISTENING: [CommandHandler("stop", stop)],
        },
        fallbacks=[
            CommandHandler("cancel", cancel),
            CommandHandler("stop", stop),
        ],
    )

    application.add_handler(conv_handler)
    application.run_polling()


if __name__ == "__main__":
    main()
