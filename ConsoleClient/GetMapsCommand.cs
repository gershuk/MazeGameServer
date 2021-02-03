using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MazeGame.Client
{
    class GetMapsCommand : ClientCommand
    {
        public override void Execute ()
        {
            var result = Client.GrpcGameServiceClient.GetMaps(new());

            foreach (var map in result.MapInfos)
            {
                Console.WriteLine("=============================================================");
                Console.WriteLine($"Map name = {map.Name}");
                Console.WriteLine($"Map guid = {map.Guid}");
                Console.WriteLine($"Map size = ({map.W},{map.H})");
                Console.WriteLine($"Map max players count = {map.Players}");
                Console.WriteLine("=============================================================");
            }
        }
    }
}
