### 📄 `docs/QUICKSTART.md`
```markdown
# Quick Start Guide

## За 5 минут

### 1. Клонировать репозиторий
```bash
git clone https://github.com/yourcompany/MobileGwDataSync.git
cd MobileGwDataSync
2. Настроить конфигурацию
bash# Копировать шаблоны
cp MobileGwDataSync.API/appsettings.template.json MobileGwDataSync.API/appsettings.json
cp MobileGwDataSync.Host/appsettings.template.json MobileGwDataSync.Host/appsettings.json

# Отредактировать настройки
# - ConnectionStrings
# - OneC credentials
3. Создать API ключ
sql-- Подключиться к SQLite (../shared/mobilegw_sync.db)
INSERT INTO api_keys (Name, KeyHash, IsActive, CreatedAt, Permissions)
VALUES ('Dev Key', '9O8KqNXBpkYvW+5cUgJ3mxr8RY5ew4H1QKxLyDtNxJg=', 1, datetime('now'), '["admin"]');
4. Запустить API
bashcd MobileGwDataSync.API
dotnet run
# API доступен на http://localhost:8080
5. Проверить работу
bash# Health check
curl http://localhost:8080/health

# API с ключом
curl -H "X-Api-Key: MasterKey123456789AbcDefGhiJklMn" \
     http://localhost:8080/api/jobs
Разработка
Требования

Visual Studio 2022 / VS Code
.NET 8 SDK
SQL Server Developer Edition
Git

Сборка
bashdotnet build
dotnet test
dotnet publish -c Release
Отладка

Открыть solution в Visual Studio
Установить Multiple Startup Projects:

MobileGwDataSync.API
MobileGwDataSync.Host


F5 для запуска

Структура проекта
MobileGwDataSync/
├── MobileGwDataSync.Core/       # Бизнес-логика
├── MobileGwDataSync.Data/       # Работа с БД
├── MobileGwDataSync.Integration/# 1C интеграция
├── MobileGwDataSync.API/        # REST API
├── MobileGwDataSync.Host/       # Windows Service
├── MobileGwDataSync.Monitoring/ # Метрики
└── MobileGwDataSync.Notifications/ # Уведомления
Docker (опционально)
Dockerfile
dockerfileFROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY ./publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "MobileGwDataSync.API.dll"]
docker-compose.yml
yamlversion: '3.8'
services:
  api:
    build: .
    ports:
      - "8080:8080"
    volumes:
      - ./shared:/app/shared
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
Тестирование
Postman коллекция
Импортировать docs/postman-collection.json
Примеры запросов
bash# Запустить синхронизацию
curl -X POST http://localhost:8080/api/sync/trigger/subscribers-sync \
     -H "X-Api-Key: MasterKey123456789AbcDefGhiJklMn"

# Получить историю
curl http://localhost:8080/api/sync/history?limit=10 \
     -H "X-Api-Key: MasterKey123456789AbcDefGhiJklMn"
Частые проблемы
"401 Unauthorized"

Проверить API ключ
Проверить хеш в БД

"403 Forbidden"

Проверить IP whitelist
Проверить permissions ключа

"Connection to 1C failed"

Проверить BaseUrl (должен заканчиваться на /)
Проверить credentials
Проверить доступность 1C

"SQL Server connection failed"

Проверить connection string
Проверить firewall
Проверить SQL Server authentication

Поддержка

Email: support@company.com
Teams: MobileGW Support Channel