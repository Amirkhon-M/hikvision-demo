import logging
import requests
from requests.auth import HTTPDigestAuth
import threading
import asyncio
import json

logger = logging.getLogger(__name__)

class HikvisionClient:
    def __init__(self, host, port, username, password, bot, chat_id, loop):
        self.host = host
        self.port = port
        self.username = username
        self.password = password
        self.base_url = f"http://{self.host}:{self.port}"
        self.auth = HTTPDigestAuth(self.username, self.password)
        self._stop_event = threading.Event()
        self.bot = bot
        self.chat_id = chat_id
        self.loop = loop

    def _test_connection(self):
        """Tests the connection to the device."""
        try:
            response = requests.get(f"{self.base_url}/ISAPI/System/deviceInfo", auth=self.auth, timeout=5)
            response.raise_for_status()
            logger.info(f"Successfully connected to {self.host}")
            return True
        except requests.exceptions.RequestException as e:
            logger.error(f"Failed to connect to {self.host}: {e}")
            raise ConnectionError(f"Failed to connect to device: {e}")

    def start_listening(self):
        """Starts listening for events from the device."""
        if not self._test_connection():
            return

        self._stop_event.clear()
        url = f"{self.base_url}/ISAPI/Event/notification/alertStream"
        try:
            with requests.get(url, auth=self.auth, stream=True, timeout=30) as response:
                response.raise_for_status()
                logger.info(f"Start listening for events from {self.host}")
                self.handle_multipart_response(response)
        except requests.exceptions.RequestException as e:
            logger.error(f"Connection to event stream failed: {e}")

    def stop_listening(self):
        """Stops the listening service."""
        self._stop_event.set()
        logger.info(f"Stopped listening for events from {self.host}")

    def handle_multipart_response(self, response):
        content_type = response.headers.get('content-type', '')
        boundary = None
        if 'multipart/mixed' in content_type:
            boundary = "--" + response.headers['content-type'].split('boundary=')[1].strip()

        if not boundary:
            logger.error("Not a multipart response or boundary not found")
            return

        buffer = b''
        for chunk in response.iter_content(chunk_size=1024):
            if self._stop_event.is_set():
                break
            buffer += chunk
            while boundary.encode() in buffer:
                part, buffer = buffer.split(boundary.encode(), 1)
                if part:
                    # The last boundary has an extra '--' at the end
                    if part.strip() == b'--':
                        break
                    self.handle_part(part.strip())

    def handle_part(self, part):
        if b'\r\n\r\n' not in part:
            return
            
        headers, body = part.split(b'\r\n\r\n', 1)
        headers = headers.decode('utf-8', errors='ignore')
        
        if 'application/json' in headers:
            try:
                # The body might have some leading/trailing non-json data
                body_str = body.decode('utf-8', errors='ignore').strip()
                # Find the start and end of the json
                start = body_str.find('{')
                end = body_str.rfind('}') + 1
                if start != -1 and end != 0:
                    json_str = body_str[start:end]
                    event_json = json.loads(json_str)
                    if event_json.get("eventType") == "AccessControllerEvent":
                        self.send_telegram_message(event_json)
            except json.JSONDecodeError as e:
                logger.error(f"Error decoding JSON: {e}")
                logger.debug(f"Problematic JSON string: {body_str}")
            except Exception as e:
                logger.error(f"Error handling part: {e}")

    def send_telegram_message(self, event):
        """Parses the event and sends a message to Telegram."""
        ip_address = event.get('ipAddress')
        date_time = event.get('dateTime')
        
        access_controller_event = event.get('AccessControllerEvent', {})
        name = access_controller_event.get('name')
        major_event_type = access_controller_event.get('majorEventType')
        sub_event_type = access_controller_event.get('subEventType')
        pic_url = event.get('ACSPic')

        # Check if any of the required fields are None
        if not all([ip_address, date_time, name, major_event_type, sub_event_type]):
            logger.info(f"Skipping message because some fields are None: {event}")
            return

        message = (
            f"**Access Controller Event**\n"
            f"**IP Address:** {ip_address}\n"
            f"**Time:** {date_time}\n"
            f"**Name:** {name}\n"
            f"**Major Event Type:** {major_event_type}\n"
            f"**Sub Event Type:** {sub_event_type}"
        )

        async def send_message_async():
            try:
                if pic_url:
                    await self.bot.send_photo(chat_id=self.chat_id, photo=pic_url, caption=message, parse_mode='Markdown')
                else:
                    await self.bot.send_message(chat_id=self.chat_id, text=message, parse_mode='Markdown')
            except Exception as e:
                logger.error(f"Error sending telegram message: {e}")

        future = asyncio.run_coroutine_threadsafe(send_message_async(), self.loop)
        try:
            future.result(timeout=10) # Wait for the coroutine to finish
        except Exception as e:
            logger.error(f"Error waiting for telegram message to be sent: {e}")
