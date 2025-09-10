from django.db import models

class AttendanceLog(models.Model):
    name = models.CharField(max_length=255)
    datetime = models.DateTimeField()
    photo = models.ImageField(upload_to='attendance_photos/')
    major_event_type = models.CharField(max_length=255)
    sub_event_type = models.CharField(max_length=255)

    def __str__(self):
        return f'{self.name} at {self.datetime}'