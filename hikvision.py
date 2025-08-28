import logging
import requests
from requests.auth import HTTPDigestAuth
import threading
import asyncio
import json
import os
import uuid

logger = logging.getLogger(__name__)

class HikvisionClient:
    def __init__(self, host, port, username, password, bot, chat_id, loop):
        self.host = host
        self.port = port
        self.username = username
        self.password = password
        self.base_url = f"http://{self.host}:{self.port}"
        self.auth = HTTPDigestAuth(self.username, self.password)
        self._stop_event = threading.Event() # Stop signal
        self.bot = bot
        self.chat_id = chat_id
        self.loop = loop
        self.temp_dir = "temp_hikvision_images" # Image storage
        os.makedirs(self.temp_dir, exist_ok=True)

    def _test_connection(self):
        try:
            response = requests.get(f"{self.base_url}/ISAPI/System/deviceInfo", auth=self.auth, timeout=5)
            response.raise_for_status()
            logger.info(f"Connected to {self.host}")
            return True
        except requests.exceptions.RequestException as e:
            logger.error(f"Connection failed: {e}")
            raise ConnectionError(f"Connection failed: {e}")

    def start_listening(self):
        if not self._test_connection():
            return

        self._stop_event.clear()
        url = f"{self.base_url}/ISAPI/Event/notification/alertStream"
        try:
            with requests.get(url, auth=self.auth, stream=True, timeout=None) as response: # Continuous stream
                response.raise_for_status()
                logger.info(f"Listening from {self.host}")
                for event_data in self.handle_multipart_response(response):
                    if self._stop_event.is_set():
                        break
                    self.process_event_data(event_data)
        except requests.exceptions.RequestException as e:
            logger.error(f"Stream failed: {e}")

    def stop_listening(self):
        self._stop_event.set()
        logger.info(f"Stopped listening from {self.host}")

    def handle_multipart_response(self, response):
        content_type_header = response.headers.get('content-type', '')
        boundary = None
        if 'multipart/mixed' in content_type_header:
            try:
                boundary = "--" + content_type_header.split('boundary=')[1].strip()
            except IndexError:
                logger.error("Boundary not found.")
                return

        if not boundary:
            logger.error("Not multipart.")
            return

        buffer = b'' # Data buffer
        current_event_parts = [] # Event parts
        
        for chunk in response.iter_content(chunk_size=4096):
            if self._stop_event.is_set():
                break
            buffer += chunk
            
            while True:
                boundary_bytes = boundary.encode('iso-8859-1') # Encode boundary
                parts = buffer.split(boundary_bytes, 1)
                
                if len(parts) < 2:
                    break
                
                part_data = parts[0].strip()
                buffer = parts[1]
                
                if part_data:
                    if b'\r\n\r\n' not in part_data:
                        continue
                    
                    headers_raw, body = part_data.split(b'\r\n\r\n', 1)
                    headers = headers_raw.decode('iso-8859-1', errors='ignore') # Decode headers
                    
                    content_type = ''
                    for line in headers.split('\r\n'):
                        if line.lower().startswith('content-type:'):
                            content_type = line.split(':', 1)[1].strip()
                            break
                    
                    if content_type:
                        part_info = {'content_type': content_type, 'body': body}
                        current_event_parts.append(part_info)
                        
                        if 'application/json' in content_type and len(current_event_parts) > 1:
                            yield current_event_parts[:-1] # Yield event
                            current_event_parts = [part_info] # Start new
                
                if buffer.startswith(b'--\r\n'):
                    if current_event_parts:
                        yield current_event_parts
                    return
                elif buffer.startswith(b'\r\n'):
                    buffer = buffer[2:]

        if current_event_parts:
            yield current_event_parts

    def process_event_data(self, event_parts):
        event_json = None
        image_paths = []

        for part in event_parts:
            content_type = part['content_type']
            body = part['body']

            if 'application/json' in content_type:
                try:
                    body_str = body.decode('utf-8', errors='ignore').strip()
                    start = body_str.find('{')
                    end = body_str.rfind('}') + 1
                    if start != -1 and end != 0:
                        json_str = body_str[start:end]
                        event_json = json.loads(json_str)
                except json.JSONDecodeError as e:
                    logger.error(f"JSON error: {e}")
                    logger.debug(f"Problematic JSON: {body_str}")
                except Exception as e:
                    logger.error(f"Part error: {e}")
            elif 'image/' in content_type:
                try:
                    filename = f"image_{uuid.uuid4().hex}.jpg"
                    filepath = os.path.join(self.temp_dir, filename)
                    with open(filepath, 'wb') as f:
                        f.write(body)
                    image_paths.append(filepath)
                    logger.info(f"Image saved: {filepath}")
                except Exception as e:
                    logger.error(f"Image save error: {e}")
        
        if event_json and event_json.get("eventType") == "AccessControllerEvent":
            self.send_telegram_message(event_json, image_paths)
        else:
            logger.debug(f"Skipping non-AccessControllerEvent: {event_json}")

    def send_telegram_message(self, event, image_paths=None):
        ip_address = event.get('ipAddress')
        date_time = event.get('dateTime')
        
        access_controller_event = event.get('AccessControllerEvent', {})
        name = access_controller_event.get('name')
        major_event_type = access_controller_event.get('majorEventType')
        sub_event_type = access_controller_event.get('subEventType')

        if not all([ip_address, date_time, name, major_event_type, sub_event_type]):
            logger.info(f"Skipping message: {event}")
            return

        message = (
            f"**Access Controller Event**\n"
            f"**IP Address:** {ip_address}\n"
            f"**Time:** {date_time}\n"
            f"**Name:** {name if name else 'N/A'}\n"
            f"**Major Event Type:** {major_event_type}\n"
            f"**Sub Event Type:** {sub_event_type}"
        )

        async def send_message_async():
            try:
                if image_paths:
                    for img_path in image_paths:
                        with open(img_path, 'rb') as photo_file:
                            await self.bot.send_photo(chat_id=self.chat_id, photo=photo_file, caption=message, parse_mode='Markdown')
                        os.remove(img_path) # Clean up
                else:
                    await self.bot.send_message(chat_id=self.chat_id, text=message, parse_mode='Markdown')
            except Exception as e:
                logger.error(f"Telegram error: {e}")

        future = asyncio.run_coroutine_threadsafe(send_message_async(), self.loop)
        try:
            future.result(timeout=10)
        except Exception as e:
            logger.error(f"Telegram send error: {e}")