using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MazeGame.Client
{
    class DeleteRoom : ClientCommand
    {
        public override void Execute ()
        {
            if (!Client.PlayerGuid.HasValue || !Client.CreatedRoomGuid.HasValue)
            {
                Console.WriteLine("You should login and create room before delete");
                return;
            }
            var result = Client.GrpcGameServiceClient.DeleteRoom(new()
            {
                PlayerGuid = new() { Guid_ = Client.PlayerGuid.Value.ToString("D") },
                RoomGuid = new() { Guid_ = Client.CreatedRoomGuid.Value.ToString("D") },
            });

            Console.WriteLine($"Guid status {result.RequestingGuidStatus}");
            Console.WriteLine(result.Status);
        }
    }
}
