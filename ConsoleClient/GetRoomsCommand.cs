using System;

namespace MazeGame.Client
{
    class GetRoomsCommand : ClientCommand
    {
        public override void Execute ()
        {
            var result = Client.GrpcGameServiceClient.GetRoomList(new());

            if (result.RoomProperties.Count == 0)
                Console.WriteLine("No rooms");

            foreach (var roomInfo in result.RoomProperties)
            {
                Console.WriteLine("=============================================================");
                Console.WriteLine($"Room name = {roomInfo.Name}");
                Console.WriteLine($"Room description = {roomInfo.Description}");
                Console.WriteLine($"Room owner name = {roomInfo.Owner}");
                Console.WriteLine($"Room has password = {roomInfo.HasPassword}");
                Console.WriteLine($"Room guid = {roomInfo.Guid.Guid_}");
                Console.WriteLine($"{roomInfo.PlayerNames.Count + roomInfo.BotTypes.Count}/{roomInfo.MaxPlayerCount}");
                Console.WriteLine($"Players {roomInfo.PlayerNames}");
                Console.WriteLine($"Bots {roomInfo.BotTypes}");
                Console.WriteLine($"Turns = {roomInfo.TurnsCount}");
                Console.WriteLine($"Deley = {roomInfo.TurnDeley}");
                Console.WriteLine($"Map = {roomInfo.MapGuid}");
                Console.WriteLine($"Status = {roomInfo.Status}");
                Console.WriteLine("=============================================================");
            }
        }
    }
}
