markdown# API Documentation - MobileGW Data Sync Service

## Аутентификация
Все API запросы требуют заголовок `X-Api-Key` с валидным ключом.
```httpX-Api-Key: your-api-key-here

## Базовый URLhttp://localhost:8080/api

## Endpoints

### Синхронизация

#### Запустить синхронизацию
```httpPOST /api/sync/trigger/{jobId}
**Параметры:**
- `jobId` - идентификатор задачи (например: "subscribers-sync")

**Rate Limit:** 5 запросов/минуту

**Ответ:**
```json{
"success": true,
"message": "Synchronization completed successfully",
"recordsProcessed": 1500,
"duration": 45.3,
"metrics": {
"runId": "guid",
"recordsFetched": 1500
}
}

#### История синхронизаций
```httpGET /api/sync/history?jobId={jobId}&limit=50&from=2024-01-01&to=2024-12-31&status=Completed
**Параметры (query):**
- `jobId` - фильтр по задаче
- `limit` - количество записей (default: 50)
- `from` - дата начала
- `to` - дата конца
- `status` - статус (Pending/InProgress/Completed/Failed)

#### Детали запуска
```httpGET /api/sync/runs/{runId}
**Ответ:** Полная информация с шагами и метриками

#### Отменить выполнение
```httpPOST /api/sync/cancel/{runId}

#### Активные синхронизации
```httpGET /api/sync/active

#### Статистика
```httpGET /api/sync/statistics?from=2024-01-01&to=2024-12-31

### Управление задачами

#### Список задач
```httpGET /api/jobs
**Cache:** 30 секунд

#### Создать задачу
```httpPOST /api/jobs
**Тело запроса:**
```json{
"name": "Sync Controllers",
"jobType": "Controllers",
"cronExpression": "0 0 * * * ?",
"isEnabled": true,
"priority": 10,
"oneCEndpoint": "controllers",
"targetProcedure": "USP_MA_MergeControllers",
"configuration": {
"batchSize": "5000"
}
}

#### Обновить задачу
```httpPUT /api/jobs/{id}

#### Удалить задачу
```httpDELETE /api/jobs/{id}

#### Включить/выключить
```httpPATCH /api/jobs/{id}/toggle

#### Запустить немедленно
```httpPOST /api/jobs/{id}/trigger
**Rate Limit:** HeavyOperation policy

### Мониторинг

#### Здоровье системы
```httpGET /api/health
**Ответ:**
```json{
"status": "Healthy",
"checks": {
"SQLite": {
"status": "Healthy",
"responseTime": 5
},
"SqlServer": {
"status": "Healthy",
"responseTime": 23
},
"OneC": {
"status": "Degraded",
"responseTime": 1500
}
}
}

#### Метрики производительности
```httpGET /api/metrics/performance?from=2024-01-01&to=2024-12-31

#### Prometheus метрики
```httpGET /metrics
**Формат:** Prometheus text format

### API Ключи (Admin)

#### Генерация ключа
```httpPOST /api/admin/apikeys/generate
**IP Restriction:** Только localhost по умолчанию

**Тело запроса:**
```json{
"name": "Integration Service",
"description": "Key for external integration",
"permissions": ["sync.read", "sync.execute"],
"expiresAt": "2025-12-31T23:59:59",
"allowedIPs": "192.168.1.100,192.168.1.101"
}

#### Список ключей
```httpGET /api/admin/apikeys/list

#### Отозвать ключ
```httpDELETE /api/admin/apikeys/{id}

#### Проверить текущий ключ
```httpGET /api/admin/apikeys/validate

### Уведомления

#### Правила оповещений
```httpGET /api/alerts/rules
POST /api/alerts/rules
PUT /api/alerts/rules/{id}
DELETE /api/alerts/rules/{id}
PATCH /api/alerts/rules/{id}/toggle

#### История оповещений
```httpGET /api/alerts/history?ruleId=1&from=2024-01-01&acknowledged=false

#### Подтвердить оповещение
```httpPOST /api/alerts/history/{id}/acknowledge

#### Тестовое оповещение
```httpPOST /api/alerts/rules/{id}/test

## Коды ответов

- `200 OK` - Успешное выполнение
- `201 Created` - Ресурс создан
- `400 Bad Request` - Ошибка валидации
- `401 Unauthorized` - Отсутствует или невалидный API ключ
- `403 Forbidden` - Недостаточно прав или IP не разрешен
- `404 Not Found` - Ресурс не найден
- `409 Conflict` - Конфликт (дубликат, задача уже выполняется)
- `429 Too Many Requests` - Превышен rate limit
- `500 Internal Server Error` - Ошибка сервера
- `503 Service Unavailable` - Сервис недоступен

## Rate Limiting

| Политика | Лимит | Окно | Применение |
|----------|-------|------|------------|
| Global | 100/мин | 1 мин | Все endpoints |
| HeavyOperation | 10/мин | 1 мин | Trigger операции |
| SyncOperations | 5/мин | 1 мин | Sync endpoints |
| ReadOperations | 60/мин | 1 мин | GET запросы |

## Permissions

| Permission | Описание |
|------------|----------|
| admin | Полный доступ |
| sync.execute | Запуск синхронизации |
| sync.read | Чтение истории |
| sync.write | Создание/изменение задач |
| alerts.manage | Управление оповещениями |