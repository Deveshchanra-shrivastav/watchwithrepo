# Popkorn Android API Documentation

Base URL: `http://YOUR_SERVER_IP:5000/api`

## Authentication
All protected endpoints require:
```
Authorization: Bearer YOUR_JWT_TOKEN
```

---

## Endpoints

### Auth

#### Register
```
POST /api/auth/register
Content-Type: application/json

{
  "displayName": "Aryan",
  "email": "aryan@example.com",
  "password": "Pass@123"
}

Response:
{
  "success": true,
  "token": "eyJhbGci...",
  "user": {
    "id": "abc123",
    "displayName": "Aryan",
    "email": "aryan@example.com",
    "avatarInitials": "AR",
    "avatarColor": "#8b7cf8"
  }
}
```

#### Login
```
POST /api/auth/login
Content-Type: application/json

{
  "email": "aryan@example.com",
  "password": "Pass@123"
}

Response: same as register
```

#### Get current user
```
GET /api/auth/me
Authorization: Bearer TOKEN

Response:
{
  "success": true,
  "user": { "id", "displayName", "email", "avatarInitials", "avatarColor" }
}
```

---

### Rooms

#### Get my rooms
```
GET /api/rooms
Authorization: Bearer TOKEN
```

#### Create room
```
POST /api/rooms
Authorization: Bearer TOKEN
Content-Type: application/json

{ "name": "Movie Night" }

Response: { "success": true, "room": { "id", "code", "name", ... } }
```

#### Join room by code
```
POST /api/rooms/join
Authorization: Bearer TOKEN
Content-Type: application/json

{ "code": "AB3X9Z" }
```

#### Get room details
```
GET /api/rooms/{code}
Authorization: Bearer TOKEN
```

---

### Chat

#### Get chat list (like WhatsApp)
```
GET /api/chat/list
Authorization: Bearer TOKEN
```

#### Get direct messages with a user
```
GET /api/chat/{userId}/messages?count=50
Authorization: Bearer TOKEN
```

#### Get room messages
```
GET /api/rooms/{roomId}/messages?count=50
Authorization: Bearer TOKEN
```

---

### Users

#### Search users
```
GET /api/users/search?q=aryan
Authorization: Bearer TOKEN
```

---

### Video Upload

#### Upload video file
```
POST /api/video/upload
Authorization: Bearer TOKEN
Content-Type: multipart/form-data

file: <video file>

Response: { "success": true, "url": "http://server/uploads/abc.mp4", "name": "video.mp4" }
```

---

### SignalR (Real-time)

#### Get SignalR connection token
```
GET /api/signalr/token
Authorization: Bearer TOKEN

Response: { "success": true, "token": "...", "hubUrl": "http://server/watchHub" }
```

#### Connect to SignalR from Android
```kotlin
// In Android (Java/Kotlin) using Microsoft SignalR library
val hubConnection = HubConnectionBuilder
    .create("http://YOUR_SERVER/watchHub?access_token=YOUR_JWT_TOKEN")
    .build()

hubConnection.start().blockingAwait()

// Join a room
hubConnection.invoke("JoinRoom", roomId)

// Listen for video sync
hubConnection.on("PlaybackSync", { state: PlaybackState ->
    // sync video player
}, PlaybackState::class.java)

// Listen for messages
hubConnection.on("NewRoomMessage", { msg: MessageDto ->
    // show message
}, MessageDto::class.java)

// Send a message
hubConnection.invoke("SendRoomMessage", roomId, encryptedText, iv)

// Play/pause/seek
hubConnection.invoke("Play",  roomId, currentTimeSeconds)
hubConnection.invoke("Pause", roomId, currentTimeSeconds)
hubConnection.invoke("Seek",  roomId, currentTimeSeconds, "seek")
```

---

### Health Check
```
GET /api/ping
Response: { "success": true, "message": "Popkorn API is running!", "version": "2.0" }
```

---

## Android Gradle dependency
```gradle
implementation 'com.microsoft.signalr:signalr:8.0.0'
implementation 'com.squareup.retrofit2:retrofit:2.9.0'
implementation 'com.squareup.retrofit2:converter-gson:2.9.0'
```

## Notes
- JWT tokens expire after 30 days
- All chat messages are E2E encrypted — encrypt/decrypt in Android using AES-256-GCM
- Video sync uses SignalR WebSocket with JWT in query string
- Max video upload size: 500 MB
