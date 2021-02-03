using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MazeGame.Client
{
    class KickCommand : ClientCommand
    {
        public override void Execute ()
        {
            Console.WriteLine("Enter player login");
            var targetLogin = Console.ReadLine().TrimEnd().TrimStart();
            var result = Client.GrpcGameServiceClient.KickPlayer(new GrpcService.KickMessage()
            {
                OwnerData = new()
                {
                    PlayerGuid = new() { Guid_ = Client.PlayerGuid.Value.ToString("D") },
                    RoomGuid = new() { Guid_ = Client.CreatedRoomGuid.Value.ToString("D") },
                },
                TargetLogin = targetLogin,
            });

            Console.WriteLine(result.Status);
        }
    }
}

