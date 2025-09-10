from django.urls import path
from . import views
from django.contrib.auth import views as auth_views

urlpatterns = [
    path('', views.index, name='index'),
    path('admin_login/', views.admin_login, name='admin_login'),
    path('attendance_logs/', views.attendance_logs, name='attendance_logs'),
    path('parent_login/', auth_views.LoginView.as_view(template_name='attendance/parent_login.html', next_page='/attendance_logs/'), name='parent_login'),
    path('parent_logout/', auth_views.LogoutView.as_view(next_page='index'), name='parent_logout'),
]