using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MazeGame.Client
{
    class StartGameCommand : ClientCommand
    {
        public override void Execute ()
        {
            if (!Client.CreatedRoomGuid.HasValue || Client.CreatedRoomGuid.Value == Guid.Empty)
            {
                Console.WriteLine("You don't have a room");
                return;
            }

            var result = Client.GrpcGameServiceClient.StartGame(new() { PlayerGuid = new() { Guid_ = Client.PlayerGuid.Value.ToString("D") }, RoomGuid = new() { Guid_ = Client.CreatedRoomGuid.Value.ToString("D") } });

            if (result.RequestingGuidStatus == GrpcService.RequestingGuidStatus.Exists)
            {
                Console.WriteLine(result.GameStatus);
            }
            else
            {
                Console.WriteLine("Player guid not exist");
            }
        }
    }
}
