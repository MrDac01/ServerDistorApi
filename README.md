Садиков А.А._Тестовое задание_SDETC#

Проект: ServerDistorApi

Реализовано API-приложение для распределения вычислительных серверов из пула.

По заданию покрыты сценарии:
1) Добавить новый сервер в пул.
2) Получить список серверов в пуле.
3) Найти свободные серверы по параметрам.
4) Взять сервер в аренду по ID.
5) Узнать готов ли сервер к выдаче (readiness).
6) Освободить сервер вручную.
7) Автоматически освободить сервер через 20 минут после выдачи.
8) Если сервер выключен, активировать аренду только через 5 минут.

- ASP.NET Core Web API (.NET 9)
- Entity Framework Core
- SQLite (локальная БД)
- ILogger (встроенное логирование)
- BackgroundService (фоновый жизненный цикл аренды)
- xUnit (тесты)

Слой API
Файл: Controller/ServerController.cs
Назначение:
- Принимает HTTP-запросы.
- Делегирует бизнес-логику в сервис.
- Возвращает HTTP-статусы и payload.

Слой бизнес-логики
Файлы:
- Services/IServerServices.cs
- Services/ServerServices.cs
Назначение:
- Правила аренды и освобождения.
- Переходы состояний серверов и аренд.
- Проверка готовности.
- Защита от конкурентных запросов.

Фоновый слой жизненного цикла
Файл: Services/RentalLifecycleService.cs
Назначение:
- Периодически проверяет аренды.
- Переводит из PendingStart в Active при наступлении ReadyAfterUtc.
- Делает автоосвобождение из Active в Released при AutoReleaseAtUtc.
- Возвращает сервер в статус Free.

Слой данных
Файл: Model/ServerContext.cs
Назначение:
- DbContext и таблицы БД.
- Индексы для ускорения поиска и lifecycle-операций.

Контракты API
Файл: Contracts/ServerContracts.cs
Назначение:
- DTO входных запросов и ответов.
- Стандартизированные результаты бизнес-ошибок.

Доменная модель

Сущности
- Server:
  Id, OS, HardwareGB, CoreCPU, Status,
  RentStartTime, RentEndTime, StartRequestTime, RentByUserId.

- Rental:
  Id, ServerId, ClientId, Status,
  RequestedAtUtc, ReadyAfterUtc,
  RentedAtUtc, AutoReleaseAtUtc, ReleasedAtUtc.

- ServerLogs:
  Id, ServerId, UserId,
  RentStart, RentEnd, WasAutoReleased.

Статусы сервера (ServerStatus)
- Free: свободен.
- Off: выключен.
- Starting: запускается.
- Rented: выдан в аренду.

Статусы аренды (RentalStatus)
- PendingStart: сервер выключен, ожидается запуск.
- Active: аренда активна.
- Released: аренда завершена.
- Rejected: зарезервировано для расширения.

Api вызовы
Базовый адрес: http://localhost:5055
Swagger: http://localhost:5055/swagger/index.html

Добавление сервера
POST /api/servers
Body:
{
  "os": "linux",
  "hardwareGB": 64,
  "coreCPU": 16,
  "isPoweredOn": false
}

Что происходит:
- Создается Server.
- Если isPoweredOn=true -> Status=Free.
- Если isPoweredOn=false -> Status=Off.
- Сервер сохраняется в БД.

Получение всех серверов
GET /api/servers

Что происходит:
- Возвращается текущий пул серверов из таблицы Servers.

Поиск свободных серверов
GET /api/servers/search?os=linux&minHardwareGb=32&minCoreCpu=8

Что происходит:
- Фильтрация по параметрам.
- В выборку идут серверы со статусом Free или Off.
  (Off допустим, так как его можно взять в аренду, но выдача будет после запуска.)

Аренда сервера
POST /api/rentals
Body:
{
  "serverId": 3,
  "clientId": "tester-1"
}

Что происходит:
- Проверяется наличие сервера.
- Проверяется, что статус Free или Off.
- Атомарно меняется статус сервера:
  - Free -> Rented (выдача сразу)
  - Off -> Starting (ожидание 5 минут)
- Создается Rental:
  - если Off: Status=PendingStart, ReadyAfterUtc=now+5m
  - если Free: Status=Active, AutoReleaseAtUtc=now+20m

Проверка готовности аренды
GET /api/rentals/{rentalId}/readiness

Что происходит:
- Если аренда PendingStart и время ReadyAfterUtc наступило,
  аренда переводится в Active, сервер в Rented,
  AutoReleaseAtUtc = now + 20 минут.
- Возвращается:
  - IsReady
  - текущий статус аренды
  - время готовности.

Ручное освобождение
POST /api/rentals/{rentalId}/release

Что происходит:
- Rental переводится в Released.
- Server переводится в Free.
- В ServerLogs фиксируется завершение аренды.


Сервер должен отключаться автоматически через 20 мин после того был выдан пользователю

Правило 5 минут (выключенный сервер)
- При аренде выключенного сервера создается Rental со статусом PendingStart.
- ReadyAfterUtc = текущий UTC + 5 минут.
- До истечения ReadyAfterUtc аренда не считается готовой к выдаче.

Правило 20 минут (автоосвобождение)
- Когда аренда становится Active, ей задается AutoReleaseAtUtc = now + 20 минут.
- Фоновый сервис проверяет истекшие аренды и автоматически переводит их в Released.
- Сервер возвращается в Free.
- Операция пишется в ServerLogs с признаком автоосвобождения.

Проблема:
- Два клиента могут одновременно попытаться арендовать один и тот же сервер.

Решение:
- Используются атомарные обновления ExecuteUpdate с условием текущего статуса.
- Обновление проходит только если сервер все еще в ожидаемом состоянии.
- Если запись не обновлена (rows == 0), возвращается конфликт состояния
  (сервер уже изменен другим запросом).

Итог:
- Из конкурентных запросов на один сервер только один становится успешным.

Используется ILogger:
- добавление сервера,
- создание аренды,
- переход PendingStart -> Active,
- ручное освобождение,
- автоосвобождение,
- ошибки фонового цикла.

Системные логи EF Core также отображают SQL-команды в Development.

Провайдер: SQLite
Строка подключения: appsettings.json -> ConnectionStrings:DefaultConnection
Файл БД: serverdistorapi.db

Таблицы:
- Servers
- Rentals
- ServerLogs

Индексы:
- Servers(Status)
- Rentals(ServerId)
- Rentals(Status)
- Rentals(ReadyAfterUtc)
- Rentals(AutoReleaseAtUtc)
- ServerLogs(ServerId)
- ServerLogs(RentEnd)

Миграции:
- InitialCreate
- RemoveUsersTable
- AddRentals
 AddQueryIndexes

Тестовый проект: tests/ServerDistorApi.Tests

Покрытые сценарии:
1) Успешная аренда включенного сервера (сразу Active).
2) Выключенный сервер: readiness только через 5 минут.
3) Конкурентная аренда одного сервера: только один успех.
4) Автоосвобождение фоновым сервисом после истечения срока.

Команда запуска:
- dotnet test tests/ServerDistorApi.Tests/ServerDistorApi.Tests.csproj (тесты)
  
1) dotnet restore
2) dotnet ef database update
3) dotnet run

Требования задания выполнены:
- API для управления пулом серверов.
- Механика аренды с учетом состояний питания.
- Проверка готовности.
- Автоматическое освобождение через 20 минут.
- Логирование.
- Хранение в БД.
- Обработка конкурентных запросов.
- Добавлены автоматические тесты.
