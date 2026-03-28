-- ============================================================
--  WatchWith Database Setup Script
--  Run this in SSMS against your SQL Server instance
--  Creates database, all tables, and all stored procedures
-- ============================================================

USE master;
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'WatchWithDB')
    CREATE DATABASE WatchWithDB;
GO

USE WatchWithDB;
GO

-- ============================================================
--  TABLES
-- ============================================================

-- ASP.NET Identity tables (required for login/register)
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='AspNetRoles' AND xtype='U')
CREATE TABLE AspNetRoles (
    Id               NVARCHAR(450) NOT NULL PRIMARY KEY,
    Name             NVARCHAR(256) NULL,
    NormalizedName   NVARCHAR(256) NULL,
    ConcurrencyStamp NVARCHAR(MAX) NULL
);

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='AspNetUsers' AND xtype='U')
CREATE TABLE AspNetUsers (
    Id                   NVARCHAR(450)  NOT NULL PRIMARY KEY,
    DisplayName          NVARCHAR(100)  NOT NULL DEFAULT '',
    AvatarInitials       NVARCHAR(5)    NOT NULL DEFAULT '',
    AvatarColor          NVARCHAR(20)   NOT NULL DEFAULT '#7F77DD',
    CreatedAt            DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
    LastSeen             DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
    UserName             NVARCHAR(256)  NULL,
    NormalizedUserName   NVARCHAR(256)  NULL,
    Email                NVARCHAR(256)  NULL,
    NormalizedEmail      NVARCHAR(256)  NULL,
    EmailConfirmed       BIT            NOT NULL DEFAULT 0,
    PasswordHash         NVARCHAR(MAX)  NULL,
    SecurityStamp        NVARCHAR(MAX)  NULL,
    ConcurrencyStamp     NVARCHAR(MAX)  NULL,
    PhoneNumber          NVARCHAR(MAX)  NULL,
    PhoneNumberConfirmed BIT            NOT NULL DEFAULT 0,
    TwoFactorEnabled     BIT            NOT NULL DEFAULT 0,
    LockoutEnd           DATETIMEOFFSET NULL,
    LockoutEnabled       BIT            NOT NULL DEFAULT 0,
    AccessFailedCount    INT            NOT NULL DEFAULT 0
);

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='AspNetUserRoles' AND xtype='U')
CREATE TABLE AspNetUserRoles (
    UserId NVARCHAR(450) NOT NULL,
    RoleId NVARCHAR(450) NOT NULL,
    PRIMARY KEY (UserId, RoleId)
);

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='AspNetUserClaims' AND xtype='U')
CREATE TABLE AspNetUserClaims (
    Id         INT           IDENTITY(1,1) PRIMARY KEY,
    UserId     NVARCHAR(450) NOT NULL,
    ClaimType  NVARCHAR(MAX) NULL,
    ClaimValue NVARCHAR(MAX) NULL
);

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='AspNetUserLogins' AND xtype='U')
CREATE TABLE AspNetUserLogins (
    LoginProvider       NVARCHAR(128) NOT NULL,
    ProviderKey         NVARCHAR(128) NOT NULL,
    ProviderDisplayName NVARCHAR(MAX) NULL,
    UserId              NVARCHAR(450) NOT NULL,
    PRIMARY KEY (LoginProvider, ProviderKey)
);

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='AspNetUserTokens' AND xtype='U')
CREATE TABLE AspNetUserTokens (
    UserId        NVARCHAR(450) NOT NULL,
    LoginProvider NVARCHAR(128) NOT NULL,
    Name          NVARCHAR(128) NOT NULL,
    Value         NVARCHAR(MAX) NULL,
    PRIMARY KEY (UserId, LoginProvider, Name)
);

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='AspNetRoleClaims' AND xtype='U')
CREATE TABLE AspNetRoleClaims (
    Id         INT           IDENTITY(1,1) PRIMARY KEY,
    RoleId     NVARCHAR(450) NOT NULL,
    ClaimType  NVARCHAR(MAX) NULL,
    ClaimValue NVARCHAR(MAX) NULL
);

-- Watch Rooms
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Rooms' AND xtype='U')
CREATE TABLE Rooms (
    Id         INT           IDENTITY(1,1) PRIMARY KEY,
    Code       NVARCHAR(10)  NOT NULL UNIQUE,
    Name       NVARCHAR(200) NOT NULL,
    OwnerId    NVARCHAR(450) NOT NULL,
    VideoUrl   NVARCHAR(MAX) NULL,
    VideoType  NVARCHAR(20)  NOT NULL DEFAULT 'none',
    CurrentTime FLOAT        NOT NULL DEFAULT 0,
    IsPlaying  BIT           NOT NULL DEFAULT 0,
    CreatedAt  DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_Rooms_Owner FOREIGN KEY (OwnerId) REFERENCES AspNetUsers(Id)
);

-- Room Members
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='RoomMembers' AND xtype='U')
CREATE TABLE RoomMembers (
    Id           INT           IDENTITY(1,1) PRIMARY KEY,
    RoomId       INT           NOT NULL,
    UserId       NVARCHAR(450) NOT NULL,
    JoinedAt     DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
    ConnectionId NVARCHAR(200) NULL,
    IsOnline     BIT           NOT NULL DEFAULT 0,
    CONSTRAINT FK_RoomMembers_Room FOREIGN KEY (RoomId) REFERENCES Rooms(Id) ON DELETE CASCADE,
    CONSTRAINT FK_RoomMembers_User FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_RoomMembers UNIQUE (RoomId, UserId)
);

-- Chat Messages (E2E encrypted)
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ChatMessages' AND xtype='U')
CREATE TABLE ChatMessages (
    Id            INT           IDENTITY(1,1) PRIMARY KEY,
    RoomId        INT           NULL,
    SenderId      NVARCHAR(450) NOT NULL,
    ReceiverId    NVARCHAR(450) NULL,  -- NULL = group/room message
    EncryptedText NVARCHAR(MAX) NOT NULL,
    Iv            NVARCHAR(MAX) NOT NULL,
    MessageType   NVARCHAR(20)  NOT NULL DEFAULT 'text',
    IsDeleted     BIT           NOT NULL DEFAULT 0,
    SentAt        DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
    IsRead        BIT           NOT NULL DEFAULT 0,
    CONSTRAINT FK_ChatMessages_Room   FOREIGN KEY (RoomId)   REFERENCES Rooms(Id) ON DELETE CASCADE,
    CONSTRAINT FK_ChatMessages_Sender FOREIGN KEY (SenderId) REFERENCES AspNetUsers(Id)
);

-- Direct message contacts
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Contacts' AND xtype='U')
CREATE TABLE Contacts (
    Id        INT           IDENTITY(1,1) PRIMARY KEY,
    UserId    NVARCHAR(450) NOT NULL,
    ContactId NVARCHAR(450) NOT NULL,
    AddedAt   DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_Contacts_User    FOREIGN KEY (UserId)    REFERENCES AspNetUsers(Id),
    CONSTRAINT FK_Contacts_Contact FOREIGN KEY (ContactId) REFERENCES AspNetUsers(Id),
    CONSTRAINT UQ_Contacts UNIQUE (UserId, ContactId)
);

-- Browser sessions (co-browsing)
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='BrowserSessions' AND xtype='U')
CREATE TABLE BrowserSessions (
    Id          INT           IDENTITY(1,1) PRIMARY KEY,
    RoomId      INT           NOT NULL,
    CurrentUrl  NVARCHAR(MAX) NOT NULL DEFAULT 'about:blank',
    StartedById NVARCHAR(450) NOT NULL,
    IsActive    BIT           NOT NULL DEFAULT 1,
    StartedAt   DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_BrowserSessions_Room FOREIGN KEY (RoomId) REFERENCES Rooms(Id) ON DELETE CASCADE
);

GO

-- ============================================================
--  INDEXES
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='IX_Rooms_Code')
    CREATE INDEX IX_Rooms_Code ON Rooms(Code);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='IX_RoomMembers_RoomId')
    CREATE INDEX IX_RoomMembers_RoomId ON RoomMembers(RoomId);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='IX_RoomMembers_UserId')
    CREATE INDEX IX_RoomMembers_UserId ON RoomMembers(UserId);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='IX_ChatMessages_RoomId')
    CREATE INDEX IX_ChatMessages_RoomId ON ChatMessages(RoomId);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='IX_ChatMessages_SenderId')
    CREATE INDEX IX_ChatMessages_SenderId ON ChatMessages(SenderId);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='IX_ChatMessages_DirectMsg')
    CREATE INDEX IX_ChatMessages_DirectMsg ON ChatMessages(SenderId, ReceiverId);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='IX_AspNetUsers_NormalizedEmail')
    CREATE UNIQUE INDEX IX_AspNetUsers_NormalizedEmail ON AspNetUsers(NormalizedEmail) WHERE NormalizedEmail IS NOT NULL;

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='IX_AspNetUsers_NormalizedUserName')
    CREATE UNIQUE INDEX IX_AspNetUsers_NormalizedUserName ON AspNetUsers(NormalizedUserName) WHERE NormalizedUserName IS NOT NULL;

GO

-- ============================================================
--  STORED PROCEDURES
-- ============================================================

-- ── Rooms ────────────────────────────────────────────────────

CREATE OR ALTER PROCEDURE sp_CreateRoom
    @Name    NVARCHAR(200),
    @OwnerId NVARCHAR(450),
    @Code    NVARCHAR(10)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO Rooms (Code, Name, OwnerId)
    VALUES (@Code, @Name, @OwnerId);

    DECLARE @RoomId INT = SCOPE_IDENTITY();

    INSERT INTO RoomMembers (RoomId, UserId)
    VALUES (@RoomId, @OwnerId);

    SELECT r.*, u.DisplayName AS OwnerName
    FROM Rooms r
    JOIN AspNetUsers u ON r.OwnerId = u.Id
    WHERE r.Id = @RoomId;
END;
GO

CREATE OR ALTER PROCEDURE sp_GetRoomByCode
    @Code NVARCHAR(10)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT r.*,
           u.DisplayName AS OwnerName,
           m.UserId      AS MemberUserId,
           m.IsOnline    AS MemberIsOnline,
           m.ConnectionId AS MemberConnectionId,
           au.DisplayName  AS MemberDisplayName,
           au.AvatarInitials AS MemberAvatar,
           au.AvatarColor    AS MemberColor
    FROM Rooms r
    JOIN AspNetUsers u  ON r.OwnerId = u.Id
    LEFT JOIN RoomMembers m ON m.RoomId = r.Id
    LEFT JOIN AspNetUsers au ON au.Id = m.UserId
    WHERE r.Code = UPPER(@Code);
END;
GO

CREATE OR ALTER PROCEDURE sp_GetRoomById
    @RoomId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT r.*,
           m.UserId       AS MemberUserId,
           m.IsOnline     AS MemberIsOnline,
           m.ConnectionId AS MemberConnectionId,
           au.DisplayName   AS MemberDisplayName,
           au.AvatarInitials AS MemberAvatar,
           au.AvatarColor    AS MemberColor
    FROM Rooms r
    LEFT JOIN RoomMembers m  ON m.RoomId = r.Id
    LEFT JOIN AspNetUsers au ON au.Id = m.UserId
    WHERE r.Id = @RoomId;
END;
GO

CREATE OR ALTER PROCEDURE sp_JoinRoom
    @RoomId INT,
    @UserId NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM RoomMembers WHERE RoomId = @RoomId AND UserId = @UserId)
        INSERT INTO RoomMembers (RoomId, UserId) VALUES (@RoomId, @UserId);
    SELECT 1 AS Success;
END;
GO

CREATE OR ALTER PROCEDURE sp_SetConnection
    @RoomId       INT,
    @UserId       NVARCHAR(450),
    @ConnectionId NVARCHAR(200),
    @IsOnline     BIT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE RoomMembers
    SET ConnectionId = CASE WHEN @IsOnline = 1 THEN @ConnectionId ELSE NULL END,
        IsOnline     = @IsOnline
    WHERE RoomId = @RoomId AND UserId = @UserId;
END;
GO

CREATE OR ALTER PROCEDURE sp_UpdatePlayback
    @RoomId      INT,
    @CurrentTime FLOAT,
    @IsPlaying   BIT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE Rooms SET CurrentTime = @CurrentTime, IsPlaying = @IsPlaying
    WHERE Id = @RoomId;
END;
GO

CREATE OR ALTER PROCEDURE sp_SetVideo
    @RoomId    INT,
    @VideoUrl  NVARCHAR(MAX),
    @VideoType NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE Rooms SET VideoUrl = @VideoUrl, VideoType = @VideoType,
                     CurrentTime = 0, IsPlaying = 0
    WHERE Id = @RoomId;
END;
GO

CREATE OR ALTER PROCEDURE sp_GetUserRooms
    @UserId NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT r.*,
           (SELECT COUNT(*) FROM RoomMembers WHERE RoomId = r.Id AND IsOnline = 1) AS OnlineCount,
           (SELECT COUNT(*) FROM RoomMembers WHERE RoomId = r.Id) AS MemberCount
    FROM Rooms r
    JOIN RoomMembers m ON m.RoomId = r.Id AND m.UserId = @UserId
    ORDER BY r.CreatedAt DESC;
END;
GO

CREATE OR ALTER PROCEDURE sp_DisconnectUser
    @UserId NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE RoomMembers SET IsOnline = 0, ConnectionId = NULL
    WHERE UserId = @UserId;

    UPDATE AspNetUsers SET LastSeen = GETUTCDATE()
    WHERE Id = @UserId;
END;
GO

-- ── Chat Messages ────────────────────────────────────────────

CREATE OR ALTER PROCEDURE sp_SaveMessage
    @RoomId       INT = NULL,
    @SenderId     NVARCHAR(450),
    @ReceiverId   NVARCHAR(450) = NULL,
    @EncryptedText NVARCHAR(MAX),
    @Iv           NVARCHAR(MAX),
    @MessageType  NVARCHAR(20) = 'text'
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO ChatMessages (RoomId, SenderId, ReceiverId, EncryptedText, Iv, MessageType)
    VALUES (@RoomId, @SenderId, @ReceiverId, @EncryptedText, @Iv, @MessageType);

    SELECT cm.*,
           u.DisplayName   AS SenderName,
           u.AvatarInitials AS SenderAvatar,
           u.AvatarColor    AS SenderColor
    FROM ChatMessages cm
    JOIN AspNetUsers u ON u.Id = cm.SenderId
    WHERE cm.Id = SCOPE_IDENTITY();
END;
GO

CREATE OR ALTER PROCEDURE sp_GetRoomMessages
    @RoomId INT,
    @Count  INT = 50
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@Count)
           cm.*,
           u.DisplayName    AS SenderName,
           u.AvatarInitials AS SenderAvatar,
           u.AvatarColor    AS SenderColor
    FROM ChatMessages cm
    JOIN AspNetUsers u ON u.Id = cm.SenderId
    WHERE cm.RoomId = @RoomId AND cm.IsDeleted = 0
    ORDER BY cm.SentAt DESC;
END;
GO

CREATE OR ALTER PROCEDURE sp_GetDirectMessages
    @UserId1 NVARCHAR(450),
    @UserId2 NVARCHAR(450),
    @Count   INT = 50
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@Count)
           cm.*,
           u.DisplayName    AS SenderName,
           u.AvatarInitials AS SenderAvatar,
           u.AvatarColor    AS SenderColor
    FROM ChatMessages cm
    JOIN AspNetUsers u ON u.Id = cm.SenderId
    WHERE cm.RoomId IS NULL
      AND ((cm.SenderId = @UserId1 AND cm.ReceiverId = @UserId2)
        OR (cm.SenderId = @UserId2 AND cm.ReceiverId = @UserId1))
      AND cm.IsDeleted = 0
    ORDER BY cm.SentAt DESC;
END;
GO

CREATE OR ALTER PROCEDURE sp_MarkMessagesRead
    @SenderId   NVARCHAR(450),
    @ReceiverId NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE ChatMessages SET IsRead = 1
    WHERE SenderId = @SenderId AND ReceiverId = @ReceiverId AND IsRead = 0;
END;
GO

CREATE OR ALTER PROCEDURE sp_GetChatList
    @UserId NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    -- Get all users who have exchanged messages with this user
    SELECT DISTINCT
        u.Id,
        u.DisplayName,
        u.AvatarInitials,
        u.AvatarColor,
        u.LastSeen,
        (SELECT TOP 1 EncryptedText FROM ChatMessages
         WHERE ((SenderId = @UserId AND ReceiverId = u.Id)
             OR (SenderId = u.Id AND ReceiverId = @UserId))
           AND RoomId IS NULL
         ORDER BY SentAt DESC) AS LastEncryptedMsg,
        (SELECT TOP 1 Iv FROM ChatMessages
         WHERE ((SenderId = @UserId AND ReceiverId = u.Id)
             OR (SenderId = u.Id AND ReceiverId = @UserId))
           AND RoomId IS NULL
         ORDER BY SentAt DESC) AS LastIv,
        (SELECT TOP 1 SentAt FROM ChatMessages
         WHERE ((SenderId = @UserId AND ReceiverId = u.Id)
             OR (SenderId = u.Id AND ReceiverId = @UserId))
           AND RoomId IS NULL
         ORDER BY SentAt DESC) AS LastMsgAt,
        (SELECT COUNT(*) FROM ChatMessages
         WHERE SenderId = u.Id AND ReceiverId = @UserId
           AND IsRead = 0 AND RoomId IS NULL) AS UnreadCount
    FROM AspNetUsers u
    WHERE u.Id != @UserId
      AND EXISTS (
          SELECT 1 FROM ChatMessages
          WHERE ((SenderId = @UserId AND ReceiverId = u.Id)
              OR (SenderId = u.Id AND ReceiverId = @UserId))
            AND RoomId IS NULL
      )
    ORDER BY LastMsgAt DESC;
END;
GO

-- ── Users ────────────────────────────────────────────────────

CREATE OR ALTER PROCEDURE sp_SearchUsers
    @Query  NVARCHAR(100),
    @UserId NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, DisplayName, AvatarInitials, AvatarColor, LastSeen
    FROM AspNetUsers
    WHERE Id != @UserId
      AND (DisplayName LIKE '%' + @Query + '%'
        OR Email LIKE '%' + @Query + '%')
    ORDER BY DisplayName;
END;
GO

CREATE OR ALTER PROCEDURE sp_GetUserById
    @UserId NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, DisplayName, AvatarInitials, AvatarColor, LastSeen, Email
    FROM AspNetUsers
    WHERE Id = @UserId;
END;
GO

-- ── Browser Session ──────────────────────────────────────────

CREATE OR ALTER PROCEDURE sp_UpsertBrowserSession
    @RoomId     INT,
    @CurrentUrl NVARCHAR(MAX),
    @UserId     NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM BrowserSessions WHERE RoomId = @RoomId AND IsActive = 1)
        UPDATE BrowserSessions SET CurrentUrl = @CurrentUrl
        WHERE RoomId = @RoomId AND IsActive = 1;
    ELSE
        INSERT INTO BrowserSessions (RoomId, CurrentUrl, StartedById)
        VALUES (@RoomId, @CurrentUrl, @UserId);

    SELECT CurrentUrl FROM BrowserSessions WHERE RoomId = @RoomId AND IsActive = 1;
END;
GO

CREATE OR ALTER PROCEDURE sp_GetBrowserSession
    @RoomId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CurrentUrl, StartedById, StartedAt
    FROM BrowserSessions
    WHERE RoomId = @RoomId AND IsActive = 1;
END;
GO

PRINT 'WatchWithDB setup complete. All tables and stored procedures created.';
GO
