using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MazeGame.Client
{
    class GetPlayerStateCommand : ClientCommand
    {
        public override void Execute ()
        {
            if (Client.State ==  ClientState.Started)
            {
                Console.WriteLine("Login before get your state");
                return;
            }

            var result = Client.GrpcGameServiceClient.GetPlayerState(new() { Guid_ = Client.PlayerGuid.Value.ToString("D") });

            Console.WriteLine($"Guid validation = {result.RequestingGuidStatus}");
            Console.WriteLine($"Player state = {result.PlayerState}");
            Console.WriteLine($"Created room guid = {result.CreatedRoomGuid}");
            Console.WriteLine($"Current room guid = {result.ConnectToRoomGuid}");
        }
    }
}
