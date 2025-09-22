# MobileGW Data Sync Service

## Описание
Сервис автоматической синхронизации данных между 1C и SQL Server с поддержкой мониторинга, уведомлений и REST API управления.

## Архитектура
- **Clean Architecture** с разделением на слои
- **Микросервисный подход** к синхронизации
- **Event-driven** уведомления
- **Метрики** для мониторинга

## Компоненты
1. **API Service** - REST API для управления
2. **Host Service** - Windows Service для фоновой синхронизации
3. **Monitoring** - Prometheus метрики
4. **Notifications** - Email/Telegram уведомления

## Технологии
- .NET 8.0, C# 12
- Entity Framework Core 8
- Quartz.NET (планировщик)
- Polly (устойчивость)
- Prometheus (метрики)
- Serilog (логирование)

## Требования
- .NET 8.0 Runtime
- SQL Server 2019+
- SQLite 3.0+
- IIS (опционально для API)

## Быстрый старт
См. [QUICKSTART.md](QUICKSTART.md)

## API документация
См. [API.md](API.md)

## Развертывание
См. [DEPLOYMENT.md](DEPLOYMENT.md)

## Лицензия
Proprietary