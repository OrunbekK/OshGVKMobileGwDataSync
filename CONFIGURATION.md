markdown# Configuration Guide - MobileGW Data Sync Service

## Полная схема конфигурации

### appsettings.json - все параметры
```json{
"ConnectionStrings": {
"SqlServer": "Connection string to SQL Server",
"SQLite": "Data Source=../shared/mobilegw_sync.db"
},"OneC": {
"BaseUrl": "https://server/gbill/hs/api/",
"Username": "string",
"Password": "string",
"Timeout": 300,
"HealthCheckEndpoint": "health",
"HealthCheckTimeout": 5
},"SyncSettings": {
"BatchSize": 20000,
"MaxParallelBatches": 3,
"TimeoutMinutes": 10,
"RetryPolicy": {
"MaxAttempts": 3,
"DelaySeconds": 30,
"MaxDelaySeconds": 300
}
},"Security": {
"ApiAccess": {
"Enabled": false,
"AllowedIPs": ["192.168.1.0/24", "10.0.0.1"]
},
"ApiKeyManagement": {
"AllowedIPs": ["127.0.0.1", "::1"],
"RequireAdminPermission": true,
"MaxKeysPerClient": 10,
"DefaultKeyExpirationDays": 365
}
},"Monitoring": {
"Prometheus": {
"Enabled": true,
"Port": 9090,
"Path": "/metrics"
},
"HealthChecks": {
"Enabled": true,
"Path": "/health"
}
},"Alerts": {
"Rules": [
{
"Name": "SyncFailure",
"Type": "SyncStatus",
"Condition": "Status == Failed",
"Severity": "Critical",
"Channels": ["email", "telegram"],
"ThrottleMinutes": 5
}
]
},"Notifications": {
"Email": {
"Enabled": false,
"SmtpServer": "smtp.gmail.com",
"Port": 587,
"UseSsl": true,
"From": "alerts@company.com",
"Recipients": ["admin@company.com"],
"Username": "user@gmail.com",
"Password": "app-specific-password"
},
"Telegram": {
"Enabled": false,
"BotToken": "123456:ABC-DEF1234ghIkl-zyx57W2v1u123ew11",
"ChatId": "-1001234567890"
}
},"Logging": {
"LogLevel": {
"Default": "Information",
"Microsoft": "Warning",
"Microsoft.Hosting.Lifetime": "Information",
"Microsoft.EntityFrameworkCore": "Warning"
}
},"Serilog": {
"Using": ["Serilog.Sinks.Console", "Serilog.Sinks.File"],
"MinimumLevel": {
"Default": "Information",
"Override": {
"Microsoft": "Warning",
"System": "Warning"
}
},
"WriteTo": [
{
"Name": "Console",
"Args": {
"theme": "Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme::Code, Serilog.Sinks.Console",
"outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
}
},
{
"Name": "File",
"Args": {
"path": "logs/log-.txt",
"rollingInterval": "Day",
"retainedFileCountLimit": 30,
"fileSizeLimitBytes": 10485760,
"rollOnFileSizeLimit": true
}
}
]
},"AllowedHosts": "*","Kestrel": {
"Endpoints": {
"Http": {
"Url": "http://0.0.0.0:8080"
}
}
}
}

## Переменные окружения

Можно переопределить через environment variables:
```bashConnection strings
ConnectionStrings__SqlServer=Server=...
ConnectionStrings__SQLite=Data Source=...1C settings
OneC__BaseUrl=https://...
OneC__Username=user
OneC__Password=passwordSecurity
Security__ApiAccess__Enabled=true
Security__ApiAccess__AllowedIPs__0=192.168.1.100Notifications
Notifications__Email__Enabled=true
Notifications__Telegram__BotToken=token

## Настройка задач синхронизации

### Через SQL
```sqlINSERT INTO sync_jobs (
Id, Name, JobType, CronExpression, IsEnabled,
OneCEndpoint, TargetProcedure, Configuration
) VALUES (
'controllers-sync',
'Синхронизация контроллеров',
'Controllers',
'0 30 * * * ?',  -- Каждые 30 минут
1,
'controllers',
'USP_MA_MergeControllers',
'{"batchSize":"5000","timeout":"600"}'
);

### CRON выражения┌───────────── секунды (0 - 59)
│ ┌───────────── минуты (0 - 59)
│ │ ┌───────────── часы (0 - 23)
│ │ │ ┌───────────── день месяца (1 - 31)
│ │ │ │ ┌───────────── месяц (1 - 12)
│ │ │ │ │ ┌───────────── день недели (0 - 6)
│ │ │ │ │ │


Примеры:
- `0 0 * * * ?` - каждый час
- `0 */5 * * * ?` - каждые 5 минут
- `0 0 2 * * ?` - каждый день в 2:00
- `0 0 8-18 * * MON-FRI` - каждый час с 8 до 18 в будни

## Email настройка

### Gmail
```json{
"Email": {
"SmtpServer": "smtp.gmail.com",
"Port": 587,
"UseSsl": true,
"Username": "your-email@gmail.com",
"Password": "app-specific-password"
}
}
Требуется [App Password](https://support.google.com/accounts/answer/185833)

### Office 365
```json{
"Email": {
"SmtpServer": "smtp.office365.com",
"Port": 587,
"UseSsl": true,
"Username": "your-email@company.com",
"Password": "password"
}
}

## Telegram настройка

1. Создать бота через @BotFather
2. Получить token
3. Добавить бота в группу
4. Получить chat_id:
```bashcurl https://api.telegram.org/bot<TOKEN>/getUpdates

## Оптимизация производительности

### SQL Server
```json{
"SyncSettings": {
"BatchSize": 50000,  // Увеличить для больших объемов
"MaxParallelBatches": 5  // Параллельная обработка
}
}

### Память
```json{
"Kestrel": {
"Limits": {
"MaxConcurrentConnections": 100,
"MaxConcurrentUpgradedConnections": 100,
"MaxRequestBodySize": 52428800
}
}
}

## Безопасность

### Production рекомендации
1. Использовать HTTPS
2. Включить IP restriction
3. Регулярно ротировать API ключи
4. Использовать отдельные учетные записи для 1C и SQL
5. Шифровать sensitive данные в appsettings