# WatchWith V2 — Full ASP.NET Core 8 App

## Features
- Login / Register (ASP.NET Identity)
- Watch rooms with YouTube, file upload, direct URL
- Real-time video sync (play/pause/seek/rewind) via SignalR
- Voice chat with WebRTC — unmute to speak, video auto-ducks
- End-to-end encrypted chat (AES-256-GCM, WebCrypto API)
- WhatsApp-style 1-on-1 and group chat with typing indicators
- In-app co-browser — browse together in real time
- SQL Server database with ALL data via stored procedures
- 500 MB video upload support

## Setup

### 1. SQL Server
Open SSMS, connect to your server, and run:
```
Database/setup.sql
```
This creates the `WatchWithDB` database, all tables, and all stored procedures.

### 2. Connection String
Edit `appsettings.json`:
```json
"Default": "Server=YOUR_SERVER;Database=WatchWithDB;Trusted_Connection=True;TrustServerCertificate=True;"
```
For SQL auth:
```json
"Default": "Server=YOUR_SERVER;Database=WatchWithDB;User Id=sa;Password=YOUR_PASSWORD;TrustServerCertificate=True;"
```

### 3. Run
```bash
dotnet restore
dotnet run
```
Open: http://localhost:5000

## Stored Procedures (all DB ops)
| SP | Purpose |
|----|---------|
| sp_CreateRoom | Create room + add owner as member |
| sp_GetRoomByCode | Get room + members by code |
| sp_GetRoomById | Get room + members by ID |
| sp_JoinRoom | Add user to room |
| sp_SetConnection | Update SignalR connection ID |
| sp_UpdatePlayback | Save play/pause state |
| sp_SetVideo | Change room video |
| sp_GetUserRooms | Dashboard room list |
| sp_DisconnectUser | Mark user offline |
| sp_SaveMessage | Save E2E encrypted message |
| sp_GetRoomMessages | Load room chat history |
| sp_GetDirectMessages | Load 1-on-1 chat |
| sp_GetChatList | WhatsApp-style chat list |
| sp_MarkMessagesRead | Mark DMs as read |
| sp_SearchUsers | Find users to chat with |
| sp_UpsertBrowserSession | Save co-browse URL |
| sp_GetBrowserSession | Get current browser URL |

## Voice Chat
- Click "Unmute" in the watch room to speak
- Video volume automatically ducks to 25% while speaking
- WebRTC peer-to-peer audio (STUN via Google)
- Everyone in the room hears you in real time

## Co-Browser
- Click the "Browser" tab in the watch room
- Navigate to any URL — everyone sees the same page
- Use the address bar or type a search term

## E2E Encryption
- Room chat: key derived from room code via PBKDF2
- Direct messages: key derived from combined user IDs
- AES-256-GCM — server stores only ciphertext
