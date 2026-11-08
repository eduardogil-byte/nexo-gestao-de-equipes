-- Executar no SSMS usando a mesma instância da configuração.
SELECT @@VERSION AS VersaoServidor;
SELECT SERVERPROPERTY('ServerName') AS NomeServidor;
SELECT name, compatibility_level FROM sys.databases WHERE name = N'NexoDb';
-- Executar depois de subir a API em Development com InitializeForClassroom=true.
USE [NexoDb];
SELECT name FROM sys.tables WHERE name IN (
    N'AspNetUsers', N'AspNetRoles', N'Projects', N'ProjectMembers',
    N'Tasks', N'TaskAssignees', N'Activities', N'Notifications', N'AuditEvents'
) ORDER BY name;
