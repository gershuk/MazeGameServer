using System;

namespace MazeGame.Client
{
    class CreateRoomCommand : ClientCommand
    {
        public override void Execute ()
        {
            //if (Client.CreatedRoomGuid != null && Client.CreatedRoomGuid.Value != Guid.Empty )
            //{
            //    Console.WriteLine("You alread got room");
            //    return;
            //}

            switch (Client.State)
            {
                case ClientState.Started:
                    Console.WriteLine("Login before create room");
                    return;
                case ClientState.InLobby:
                    Console.WriteLine("You already in lobby");
                    break;
                case ClientState.InGame:
                    Console.WriteLine("You already in game");
                    return;
            }

            try
            {
                Console.WriteLine("Enter room name");
                var name = Console.ReadLine();
                Console.WriteLine("Enter description");
                var description = Console.ReadLine();
                Console.WriteLine("Enter password (empty = no password)");
                var password = Console.ReadLine();
                password = password.TrimEnd().TrimStart();
                if (password == string.Empty)
                    password = null;
                Console.WriteLine("Enter map guid");
                var guid = Console.ReadLine();
                Console.WriteLine("Enter player count");
                var playerCount = Convert.ToUInt32(Console.ReadLine());
                Console.WriteLine("Enter bot types separated by a comma (example1,example2)");
                var botsString = Console.ReadLine();
                string[] bots = new string[0];
                if (botsString != null && botsString != string.Empty) 
                    bots = botsString.Trim(',').Split(',', StringSplitOptions.RemoveEmptyEntries);
                Console.WriteLine("Enter turn deley in ms");
                var turnDeley = Convert.ToUInt32(Console.ReadLine());
                Console.WriteLine("Enter turn count");
                var turnCount = Convert.ToUInt32(Console.ReadLine());

                GrpcService.OwnerRoomConfiguration config = new()
                {
                    OwnerGuid = new() { Guid_ = Client.PlayerGuid.Value.ToString("D") },
                    Properties = new()
                    {
                        Name = name,
                        Description = description,
                        HasPassword = password != null,
                        Password = password ?? string.Empty,
                        MapGuid = new() { Guid_ = guid },
                        MaxPlayerCount = playerCount,
                        TurnDeley = turnDeley,
                        TurnsCount = turnCount,
                    }
                };

                config.Properties.BotTypes.Add(bots);

                var result = Client.GrpcGameServiceClient.CreateRoom(config);
                Console.WriteLine($"Guid validation = {result.RequestingGuidStatus}");
                Console.WriteLine($"Operation result = {result.Status}");

                if (result.RequestingGuidStatus == GrpcService.RequestingGuidStatus.Exists &&
                    result.Status == GrpcService.RoomTableModificationStatus.RoomTableModificationSuccessfull)
                {
                    Console.WriteLine($"Room guid = {result.RoomGuid}");
                    Client.CreatedRoomGuid = new Guid(result.RoomGuid.Guid_);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
    }
}
