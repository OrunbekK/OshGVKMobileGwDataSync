# MobileGW Data Sync Service

## ��������
������ �������������� ������������� ������ ����� 1C � SQL Server � ���������� �����������, ����������� � REST API ����������.

## �����������
- **Clean Architecture** � ����������� �� ����
- **�������������� ������** � �������������
- **Event-driven** �����������
- **�������** ��� �����������

## ����������
1. **API Service** - REST API ��� ����������
2. **Host Service** - Windows Service ��� ������� �������������
3. **Monitoring** - Prometheus �������
4. **Notifications** - Email/Telegram �����������

## ����������
- .NET 8.0, C# 12
- Entity Framework Core 8
- Quartz.NET (�����������)
- Polly (������������)
- Prometheus (�������)
- Serilog (�����������)

## ����������
- .NET 8.0 Runtime
- SQL Server 2019+
- SQLite 3.0+
- IIS (����������� ��� API)

## ������� �����
��. [QUICKSTART.md](QUICKSTART.md)

## API ������������
��. [API.md](API.md)

## �������������
��. [DEPLOYMENT.md](DEPLOYMENT.md)

## ��������
Proprietary