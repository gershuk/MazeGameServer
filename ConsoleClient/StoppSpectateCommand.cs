using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MazeGame.Client
{
    class StoppSpectateCommand : ClientCommand
    {
        public override void Execute ()
        {
            Console.WriteLine("Enter room guid");
            var roomGuid = Console.ReadLine();
            Console.WriteLine("Enter room password");
            var password = Console.ReadLine();
            var result = Client.GrpcGameServiceClient.StopSpectateGame(new() { PlayerGuid = new() { Guid_ = Client.PlayerGuid.Value.ToString("D") }, RoomGuid = new() { Guid_ = roomGuid }, Password = password });

            Console.WriteLine(result.RequestingGuidStatus);
            Console.WriteLine(result.StopSpectateGameState);
        }
    }
}
