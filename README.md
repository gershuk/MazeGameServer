# MazeGameServer 
*Выполнили : Глушкова Диана и Гершук Владислав*
## Описание 
Учебный проект по создание сетевой игры. Цель игры выбраться из лабиринта за оперделённое число ходов.
## Как собрать 
1. Скачать и уставновить MS SQL Server [download](https://www.microsoft.com/ru-ru/sql-server/sql-server-downloads).
2. Скачать Dotnet SDK версии 5 или выше [download](https://dotnet.microsoft.com/download/dotnet/5.0).
3. Перейтив в папку репозитория и ввести команду
   ```Console  
     dotnet build -c Release -o Build 
    ```  
4. Скопировать папку Maps в Build
5. В MS SQL создать базу данных "usersdb". Создать в ней таблицу "UsersData" 

| Primary Key | Column Name        | Data Type          | Allow Nulls |
|-----| ------------- |:-------------:| -----:|
| [x]| Login     | nvarchar(50) | [ ] |
|[ ] |Password      | nchar(50)      |   [ ]|


## ConsoleClient
Приложение для взаимодействия с сервером в режиме консоли.
При запуске требуется ввести адрес и порт. (По умолчанию "127.0.0.1:30051")

Поддерживаемый список команд:
- Login
- Disconnect
- Register
- GetRooms
- GetBots
- GetMaps
- GetMyState
- ShowMyState
- CreateRoom
- ConnectToRoom
- StartGame
- SpectateGame
- Kick
- DeleteRoom
- StopSpectate
- W
- S
- A
- D



## MazeGameServer
Сервер игры. Отвечает за создание и управление игровых комнат.

При запуске требуется ввести адрес и порт. (По умолчанию "localhost:30051"). Выключается любым вводом в консоль.

## Api

Взаимодействие с сервером осуществляется через GRPC [docs]([https://link](https://grpc.io/docs/)).
Описание функций находится внутри файла "MazeGameServer\GrpcDescription\GrpcConfig.proto"