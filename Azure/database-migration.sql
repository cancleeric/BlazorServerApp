-- Azure SQL Database Migration Script
-- This script creates the database schema for Credit Monitoring System

-- Create database (if running as admin)
-- CREATE DATABASE CreditMonitoringDB;
-- GO

-- Use the database
-- USE CreditMonitoringDB;
-- GO

-- Create Users table
CREATE TABLE Users (
    Id int IDENTITY(1,1) PRIMARY KEY,
    Username nvarchar(50) NOT NULL UNIQUE,
    Email nvarchar(100) NOT NULL UNIQUE,
    PasswordHash nvarchar(256) NOT NULL,
    FirstName nvarchar(50) NOT NULL,
    LastName nvarchar(50) NOT NULL,
    Role nvarchar(20) NOT NULL DEFAULT 'User',
    IsActive bit NOT NULL DEFAULT 1,
    CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt datetime2 NOT NULL DEFAULT GETUTCDATE(),
    LastLoginAt datetime2 NULL
);

-- Create index on Users
CREATE INDEX IX_Users_Username ON Users(Username);
CREATE INDEX IX_Users_Email ON Users(Email);
CREATE INDEX IX_Users_Role ON Users(Role);

-- Create CreditAccounts table
CREATE TABLE CreditAccounts (
    Id int IDENTITY(1,1) PRIMARY KEY,
    AccountNumber nvarchar(20) NOT NULL UNIQUE,
    CustomerName nvarchar(100) NOT NULL,
    CreditLimit decimal(18,2) NOT NULL,
    CurrentBalance decimal(18,2) NOT NULL DEFAULT 0,
    Status nvarchar(20) NOT NULL DEFAULT 'Active',
    RiskLevel nvarchar(10) NOT NULL DEFAULT 'Low',
    CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt datetime2 NOT NULL DEFAULT GETUTCDATE(),
    LastActivityDate datetime2 NULL
);

-- Create indexes on CreditAccounts
CREATE INDEX IX_CreditAccounts_AccountNumber ON CreditAccounts(AccountNumber);
CREATE INDEX IX_CreditAccounts_Status ON CreditAccounts(Status);
CREATE INDEX IX_CreditAccounts_RiskLevel ON CreditAccounts(RiskLevel);
CREATE INDEX IX_CreditAccounts_CreatedAt ON CreditAccounts(CreatedAt);

-- Create CreditAlerts table
CREATE TABLE CreditAlerts (
    Id int IDENTITY(1,1) PRIMARY KEY,
    AccountId int NOT NULL,
    Severity nvarchar(10) NOT NULL,
    Message nvarchar(500) NOT NULL,
    IsResolved bit NOT NULL DEFAULT 0,
    CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE(),
    ResolvedAt datetime2 NULL,
    ResolvedBy nvarchar(50) NULL,
    Notes nvarchar(1000) NULL,
    
    CONSTRAINT FK_CreditAlerts_AccountId FOREIGN KEY (AccountId) 
        REFERENCES CreditAccounts(Id) ON DELETE CASCADE
);

-- Create indexes on CreditAlerts
CREATE INDEX IX_CreditAlerts_AccountId ON CreditAlerts(AccountId);
CREATE INDEX IX_CreditAlerts_Severity ON CreditAlerts(Severity);
CREATE INDEX IX_CreditAlerts_IsResolved ON CreditAlerts(IsResolved);
CREATE INDEX IX_CreditAlerts_CreatedAt ON CreditAlerts(CreatedAt);

-- Create AuditLogs table
CREATE TABLE AuditLogs (
    Id int IDENTITY(1,1) PRIMARY KEY,
    UserId int NULL,
    Action nvarchar(100) NOT NULL,
    EntityType nvarchar(50) NOT NULL,
    EntityId nvarchar(50) NULL,
    OldValues nvarchar(max) NULL,
    NewValues nvarchar(max) NULL,
    IpAddress nvarchar(45) NULL,
    UserAgent nvarchar(500) NULL,
    CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE(),
    
    CONSTRAINT FK_AuditLogs_UserId FOREIGN KEY (UserId) 
        REFERENCES Users(Id) ON DELETE SET NULL
);

-- Create indexes on AuditLogs
CREATE INDEX IX_AuditLogs_UserId ON AuditLogs(UserId);
CREATE INDEX IX_AuditLogs_Action ON AuditLogs(Action);
CREATE INDEX IX_AuditLogs_EntityType ON AuditLogs(EntityType);
CREATE INDEX IX_AuditLogs_CreatedAt ON AuditLogs(CreatedAt);

-- Create Reports table
CREATE TABLE Reports (
    Id int IDENTITY(1,1) PRIMARY KEY,
    Name nvarchar(100) NOT NULL,
    Type nvarchar(50) NOT NULL,
    Parameters nvarchar(max) NULL,
    Status nvarchar(20) NOT NULL DEFAULT 'Pending',
    FilePath nvarchar(500) NULL,
    CreatedBy int NOT NULL,
    CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE(),
    CompletedAt datetime2 NULL,
    ExpiresAt datetime2 NULL,
    
    CONSTRAINT FK_Reports_CreatedBy FOREIGN KEY (CreatedBy) 
        REFERENCES Users(Id) ON DELETE CASCADE
);

-- Create indexes on Reports
CREATE INDEX IX_Reports_CreatedBy ON Reports(CreatedBy);
CREATE INDEX IX_Reports_Status ON Reports(Status);
CREATE INDEX IX_Reports_Type ON Reports(Type);
CREATE INDEX IX_Reports_CreatedAt ON Reports(CreatedAt);

-- Insert default admin user (password: Admin123!)
-- Note: In production, this should be done through the application
INSERT INTO Users (Username, Email, PasswordHash, FirstName, LastName, Role)
VALUES (
    'admin',
    'admin@creditmonitoring.com',
    'AQAAAAEAACcQAAAAEKqJ8z9FtR7LbJp8N3QqH1vWJ0W2LZOw8qY5uN9mK6xP7tE1fG4hR3sV8cZ2bA1mN0',
    'System',
    'Administrator',
    'Admin'
);

-- Insert sample credit officer user
INSERT INTO Users (Username, Email, PasswordHash, FirstName, LastName, Role)
VALUES (
    'creditofficer',
    'officer@creditmonitoring.com',
    'AQAAAAEAACcQAAAAEKqJ8z9FtR7LbJp8N3QqH1vWJ0W2LZOw8qY5uN9mK6xP7tE1fG4hR3sV8cZ2bA1mN0',
    'Credit',
    'Officer',
    'CreditOfficer'
);

-- Insert sample data for testing
INSERT INTO CreditAccounts (AccountNumber, CustomerName, CreditLimit, CurrentBalance, RiskLevel)
VALUES 
    ('ACC001', 'John Smith', 10000.00, 2500.00, 'Low'),
    ('ACC002', 'Jane Doe', 15000.00, 8500.00, 'Medium'),
    ('ACC003', 'Bob Johnson', 5000.00, 4800.00, 'High'),
    ('ACC004', 'Alice Brown', 20000.00, 1200.00, 'Low'),
    ('ACC005', 'Charlie Wilson', 8000.00, 7200.00, 'High');

-- Insert sample alerts
INSERT INTO CreditAlerts (AccountId, Severity, Message)
VALUES 
    (3, 'High', 'Account balance approaching credit limit'),
    (5, 'Critical', 'Account balance exceeds 90% of credit limit'),
    (2, 'Medium', 'Unusual spending pattern detected');

-- Create stored procedures for common operations

-- Procedure to get active alerts by severity
CREATE PROCEDURE GetActiveAlertsBySeverity
    @Severity NVARCHAR(10) = NULL
AS
BEGIN
    SELECT a.*, ca.AccountNumber, ca.CustomerName
    FROM CreditAlerts a
    INNER JOIN CreditAccounts ca ON a.AccountId = ca.Id
    WHERE a.IsResolved = 0
        AND (@Severity IS NULL OR a.Severity = @Severity)
    ORDER BY a.CreatedAt DESC;
END;
GO

-- Procedure to resolve alert
CREATE PROCEDURE ResolveAlert
    @AlertId INT,
    @ResolvedBy NVARCHAR(50),
    @Notes NVARCHAR(1000) = NULL
AS
BEGIN
    UPDATE CreditAlerts
    SET IsResolved = 1,
        ResolvedAt = GETUTCDATE(),
        ResolvedBy = @ResolvedBy,
        Notes = @Notes
    WHERE Id = @AlertId;
END;
GO

-- Procedure to get account summary
CREATE PROCEDURE GetAccountSummary
    @AccountId INT = NULL
AS
BEGIN
    SELECT 
        ca.*,
        (SELECT COUNT(*) FROM CreditAlerts WHERE AccountId = ca.Id AND IsResolved = 0) AS ActiveAlerts,
        CASE 
            WHEN ca.CurrentBalance / ca.CreditLimit > 0.9 THEN 'Critical'
            WHEN ca.CurrentBalance / ca.CreditLimit > 0.7 THEN 'High'
            WHEN ca.CurrentBalance / ca.CreditLimit > 0.5 THEN 'Medium'
            ELSE 'Low'
        END AS UtilizationRisk
    FROM CreditAccounts ca
    WHERE (@AccountId IS NULL OR ca.Id = @AccountId)
    ORDER BY ca.CreatedAt DESC;
END;
GO

-- Create views for reporting

-- View for alert statistics
CREATE VIEW AlertStatistics AS
SELECT 
    Severity,
    COUNT(*) AS TotalAlerts,
    SUM(CASE WHEN IsResolved = 1 THEN 1 ELSE 0 END) AS ResolvedAlerts,
    SUM(CASE WHEN IsResolved = 0 THEN 1 ELSE 0 END) AS ActiveAlerts,
    AVG(CASE WHEN IsResolved = 1 THEN DATEDIFF(HOUR, CreatedAt, ResolvedAt) ELSE NULL END) AS AvgResolutionTimeHours
FROM CreditAlerts
GROUP BY Severity;
GO

-- View for account risk summary
CREATE VIEW AccountRiskSummary AS
SELECT 
    RiskLevel,
    COUNT(*) AS AccountCount,
    SUM(CreditLimit) AS TotalCreditLimit,
    SUM(CurrentBalance) AS TotalBalance,
    AVG(CurrentBalance / CreditLimit) AS AvgUtilization
FROM CreditAccounts
WHERE Status = 'Active'
GROUP BY RiskLevel;
GO

-- Enable row-level security (optional, for multi-tenant scenarios)
-- ALTER TABLE CreditAccounts ENABLE ROW LEVEL SECURITY;
-- ALTER TABLE CreditAlerts ENABLE ROW LEVEL SECURITY;

PRINT 'Database schema created successfully!';
PRINT 'Default admin user created with username: admin';
PRINT 'Sample data inserted for testing purposes.';
