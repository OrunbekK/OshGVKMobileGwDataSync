markdown# Deployment Guide - MobileGW Data Sync Service

## Системные требования

### Минимальные
- Windows Server 2019 / Windows 10
- .NET 8.0 Runtime
- 2 CPU cores
- 4 GB RAM
- 10 GB свободного места

### Рекомендуемые
- Windows Server 2022
- .NET 8.0 Runtime
- 4 CPU cores
- 8 GB RAM
- 20 GB SSD

## Подготовка окружения

### 1. Установка .NET 8
```powershellСкачать и установить
winget install Microsoft.DotNet.Runtime.8

### 2. SQL Server настройка

#### Создание базы данных
```sqlCREATE DATABASE OshGVKMA;
GO
USE OshGVKMA;
GO

#### Создание TVP типов
```sqlCREATE TYPE dbo.SubscriberTVP AS TABLE
(
Account NVARCHAR(50) PRIMARY KEY,
Subscriber NVARCHAR(200),
Address NVARCHAR(500),
Balance DECIMAL(18,2),
Type TINYINT,
State NVARCHAR(50),
ControllerId NVARCHAR(50),
RouteId NVARCHAR(50)
);CREATE TYPE dbo.ControllerTVP AS TABLE
(
UID UNIQUEIDENTIFIER PRIMARY KEY,
Controller NVARCHAR(200),
ControllerId NVARCHAR(50)
);

#### Создание хранимых процедур
```sqlCREATE PROCEDURE USP_MA_MergeSubscribers
@Subscribers dbo.SubscriberTVP READONLY,
@ProcessedCount INT OUTPUT,
@InsertedCount INT OUTPUT,
@UpdatedCount INT OUTPUT,
@DeletedCount INT OUTPUT
AS
BEGIN
SET NOCOUNT ON;MERGE TblRefsSubscribers AS target
USING @Subscribers AS source
ON target.Account = source.Account
WHEN MATCHED AND (
    target.Subscriber != source.Subscriber OR
    target.Address != source.Address OR
    target.Balance != source.Balance
) THEN UPDATE SET
    Subscriber = source.Subscriber,
    Address = source.Address,
    Balance = source.Balance,
    UpdatedAt = GETDATE()
WHEN NOT MATCHED THEN
    INSERT (Account, Subscriber, Address, Balance, Type, State, CreatedAt)
    VALUES (source.Account, source.Subscriber, source.Address, 
            source.Balance, source.Type, source.State, GETDATE());SET @ProcessedCount = @@ROWCOUNT;
SET @InsertedCount = 0; -- Implement counting logic
SET @UpdatedCount = 0;
SET @DeletedCount = 0;
END;CREATE PROCEDURE USP_MA_TestConnection
AS
BEGIN
SELECT 1 AS Connected;
END;

### 3. Подготовка файлов

Структура папок:C:\Services\MobileGwSync
├── API
│   ├── MobileGwDataSync.API.exe
│   ├── appsettings.json
│   └── logs
├── Host
│   ├── MobileGwDataSync.Host.exe
│   ├── appsettings.json
│   └── logs
└── shared
└── mobilegw_sync.db

## Развертывание API Service

### Вариант 1: IIS

#### Установка IIS
```powershellEnable-WindowsOptionalFeature -Online -FeatureName IIS-WebServerRole, IIS-WebServer, IIS-CommonHttpFeatures, IIS-HttpErrors, IIS-HttpRedirect, IIS-ApplicationDevelopment, IIS-HealthAndDiagnostics, IIS-HttpLogging, IIS-Security, IIS-RequestFiltering, IIS-Performance, IIS-WebServerManagementTools, IIS-ManagementConsole, IIS-IIS6ManagementCompatibility, IIS-Metabase

#### Установка ASP.NET Core Hosting Bundle
```powershellСкачать с Microsoft
https://dotnet.microsoft.com/download/dotnet/8.0/runtime

#### Настройка сайта в IIS
1. Создать Application Pool (No Managed Code)
2. Создать сайт, указать на папку API
3. Настроить порт (8080)
4. Установить права на папку logs

### Вариант 2: Kestrel (Standalone)

#### Запуск как консольное приложение
```powershellcd C:\Services\MobileGwSync\API
.\MobileGwDataSync.API.exe --urls="http://0.0.0.0:8080"

#### Создание Windows Service
```powershellsc.exe create "MobileGwSyncAPI" binPath="C:\Services\MobileGwSync\API\MobileGwDataSync.API.exe" DisplayName="MobileGW Sync API" start=auto
sc.exe description "MobileGwSyncAPI" "REST API для управления синхронизацией данных"

## Развертывание Host Service

### Установка как Windows Service
```powershellСоздание службы
sc.exe create "MobileGwSyncHost" binPath="C:\Services\MobileGwSync\Host\MobileGwDataSync.Host.exe" DisplayName="MobileGW Sync Host" start=autoОписание
sc.exe description "MobileGwSyncHost" "Фоновый сервис синхронизации 1C и SQL Server"Настройка восстановления при сбое
sc.exe failure "MobileGwSyncHost" reset=86400 actions=restart/60000/restart/60000/restart/60000Запуск
sc.exe start "MobileGwSyncHost"

## Конфигурация

### appsettings.json для Production
```json{
"ConnectionStrings": {
"SqlServer": "Server=PROD-SQL;Database=OshGVKMA;User Id=sync_user;Password=StrongP@ssw0rd;Encrypt=true;TrustServerCertificate=true;",
"SQLite": "Data Source=C:\Services\MobileGwSync\shared\mobilegw_sync.db"
},
"OneC": {
"BaseUrl": "https://1c-prod.company.local/gbill/hs/api/",
"Username": "sync_service",
"Password": "SecureP@ssw0rd",
"Timeout": 300
},
"Logging": {
"LogLevel": {
"Default": "Information",
"Microsoft": "Warning"
}
},
"Serilog": {
"MinimumLevel": {
"Default": "Information",
"Override": {
"Microsoft": "Warning"
}
},
"WriteTo": [
{
"Name": "File",
"Args": {
"path": "logs\log-.txt",
"rollingInterval": "Day",
"retainedFileCountLimit": 30
}
}
]
}
}

## Firewall правила
```powershellAPI порт
New-NetFirewallRule -DisplayName "MobileGW Sync API" -Direction Inbound -Protocol TCP -LocalPort 8080 -Action AllowPrometheus метрики
New-NetFirewallRule -DisplayName "MobileGW Metrics" -Direction Inbound -Protocol TCP -LocalPort 9090 -Action Allow

## Мониторинг

### Prometheus настройка
```yamlprometheus.yml
scrape_configs:

job_name: 'mobilegw-sync'
static_configs:

targets: ['localhost:8080']
metrics_path: '/metrics'




### Grafana Dashboard
Импортировать `docs/grafana-dashboard.json`

## Backup

### SQLite база
```powershellBackup скрипт
$source = "C:\Services\MobileGwSync\shared\mobilegw_sync.db"
backup = "C:\Backups\mobilegw_sync_
(Get-Date -Format 'yyyyMMdd_HHmmss').db"
Copy-Item $source $backup


### Scheduled Task для backup
```powershell$action = New-ScheduledTaskAction -Execute "Powershell.exe" -Argument "-File C:\Scripts\backup.ps1"
$trigger = New-ScheduledTaskTrigger -Daily -At 2:00AM
Register-ScheduledTask -TaskName "MobileGwSyncBackup" -Action $action -Trigger $trigger

## Troubleshooting

### Логи
- API: `C:\Services\MobileGwSync\API\logs\`
- Host: `C:\Services\MobileGwSync\Host\logs\`

### Проверка служб
```powershellGet-Service MobileGwSync* | Format-Table Name, Status, StartType

### Тест подключения к 1C
```bashcurl -X GET "http://localhost:8080/api/health" -H "X-Api-Key: your-key"

## Обновление

1. Остановить службы
```powershellStop-Service MobileGwSyncHost
Stop-Service MobileGwSyncAPI

2. Backup БД и конфигов

3. Заменить exe файлы

4. Запустить службы
```powershellStart-Service MobileGwSyncAPI
Start-Service MobileGwSyncHost
