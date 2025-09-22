markdown# API Documentation - MobileGW Data Sync Service

## ��������������
��� API ������� ������� ��������� `X-Api-Key` � �������� ������.
```httpX-Api-Key: your-api-key-here

## ������� URLhttp://localhost:8080/api

## Endpoints

### �������������

#### ��������� �������������
```httpPOST /api/sync/trigger/{jobId}
**���������:**
- `jobId` - ������������� ������ (��������: "subscribers-sync")

**Rate Limit:** 5 ��������/������

**�����:**
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

#### ������� �������������
```httpGET /api/sync/history?jobId={jobId}&limit=50&from=2024-01-01&to=2024-12-31&status=Completed
**��������� (query):**
- `jobId` - ������ �� ������
- `limit` - ���������� ������� (default: 50)
- `from` - ���� ������
- `to` - ���� �����
- `status` - ������ (Pending/InProgress/Completed/Failed)

#### ������ �������
```httpGET /api/sync/runs/{runId}
**�����:** ������ ���������� � ������ � ���������

#### �������� ����������
```httpPOST /api/sync/cancel/{runId}

#### �������� �������������
```httpGET /api/sync/active

#### ����������
```httpGET /api/sync/statistics?from=2024-01-01&to=2024-12-31

### ���������� ��������

#### ������ �����
```httpGET /api/jobs
**Cache:** 30 ������

#### ������� ������
```httpPOST /api/jobs
**���� �������:**
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

#### �������� ������
```httpPUT /api/jobs/{id}

#### ������� ������
```httpDELETE /api/jobs/{id}

#### ��������/���������
```httpPATCH /api/jobs/{id}/toggle

#### ��������� ����������
```httpPOST /api/jobs/{id}/trigger
**Rate Limit:** HeavyOperation policy

### ����������

#### �������� �������
```httpGET /api/health
**�����:**
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

#### ������� ������������������
```httpGET /api/metrics/performance?from=2024-01-01&to=2024-12-31

#### Prometheus �������
```httpGET /metrics
**������:** Prometheus text format

### API ����� (Admin)

#### ��������� �����
```httpPOST /api/admin/apikeys/generate
**IP Restriction:** ������ localhost �� ���������

**���� �������:**
```json{
"name": "Integration Service",
"description": "Key for external integration",
"permissions": ["sync.read", "sync.execute"],
"expiresAt": "2025-12-31T23:59:59",
"allowedIPs": "192.168.1.100,192.168.1.101"
}

#### ������ ������
```httpGET /api/admin/apikeys/list

#### �������� ����
```httpDELETE /api/admin/apikeys/{id}

#### ��������� ������� ����
```httpGET /api/admin/apikeys/validate

### �����������

#### ������� ����������
```httpGET /api/alerts/rules
POST /api/alerts/rules
PUT /api/alerts/rules/{id}
DELETE /api/alerts/rules/{id}
PATCH /api/alerts/rules/{id}/toggle

#### ������� ����������
```httpGET /api/alerts/history?ruleId=1&from=2024-01-01&acknowledged=false

#### ����������� ����������
```httpPOST /api/alerts/history/{id}/acknowledge

#### �������� ����������
```httpPOST /api/alerts/rules/{id}/test

## ���� �������

- `200 OK` - �������� ����������
- `201 Created` - ������ ������
- `400 Bad Request` - ������ ���������
- `401 Unauthorized` - ����������� ��� ���������� API ����
- `403 Forbidden` - ������������ ���� ��� IP �� ��������
- `404 Not Found` - ������ �� ������
- `409 Conflict` - �������� (��������, ������ ��� �����������)
- `429 Too Many Requests` - �������� rate limit
- `500 Internal Server Error` - ������ �������
- `503 Service Unavailable` - ������ ����������

## Rate Limiting

| �������� | ����� | ���� | ���������� |
|----------|-------|------|------------|
| Global | 100/��� | 1 ��� | ��� endpoints |
| HeavyOperation | 10/��� | 1 ��� | Trigger �������� |
| SyncOperations | 5/��� | 1 ��� | Sync endpoints |
| ReadOperations | 60/��� | 1 ��� | GET ������� |

## Permissions

| Permission | �������� |
|------------|----------|
| admin | ������ ������ |
| sync.execute | ������ ������������� |
| sync.read | ������ ������� |
| sync.write | ��������/��������� ����� |
| alerts.manage | ���������� ������������ |