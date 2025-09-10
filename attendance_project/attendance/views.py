from django.shortcuts import render, redirect
from django.http import HttpResponse
from django.contrib.auth.decorators import login_required
from .hikvision_client import HikvisionClient
from .models import AttendanceLog
import threading

# Global variable to hold the HikvisionClient instance and its listening status
hikvision_listener = {'client': None, 'is_listening': False, 'thread': None}

def index(request):
    return render(request, 'attendance/index.html')

def admin_login(request):
    if request.method == 'POST':
        action = request.POST.get('action')
        if action == 'start':
            username = request.POST.get('username')
            password = request.POST.get('password')
            device_ip = request.POST.get('device_ip')
            port = request.POST.get('port')

            if hikvision_listener['is_listening']:
                message = "Already listening."
            else:
                try:
                    client = HikvisionClient(host=device_ip, port=port, username=username, password=password)
                    hikvision_listener['client'] = client
                    hikvision_listener['thread'] = threading.Thread(target=client.start_listening)
                    hikvision_listener['thread'].daemon = True
                    hikvision_listener['thread'].start()
                    hikvision_listener['is_listening'] = True
                    message = "Started listening for events."
                except ConnectionError as e:
                    message = f"Connection failed: {e}"
                except Exception as e:
                    message = f"An error occurred: {e}"
        elif action == 'stop':
            if hikvision_listener['is_listening'] and hikvision_listener['client']:
                hikvision_listener['client'].stop_listening()
                hikvision_listener['thread'].join(timeout=5) # Wait for thread to finish
                hikvision_listener['client'] = None
                hikvision_listener['is_listening'] = False
                hikvision_listener['thread'] = None
                message = "Stopped listening."
            else:
                message = "Not currently listening."
        return render(request, 'attendance/admin_login.html', {'message': message, 'is_listening': hikvision_listener['is_listening']})
    
    return render(request, 'attendance/admin_login.html', {'is_listening': hikvision_listener['is_listening']})

@login_required
def attendance_logs(request):
    logs = AttendanceLog.objects.all().order_by('-datetime')
    return render(request, 'attendance/attendance_logs.html', {'logs': logs})