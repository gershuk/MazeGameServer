using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Google.Protobuf.WellKnownTypes;

namespace MazeGame.Client
{
    class GetBotsCommand : ClientCommand
    {
        public override void Execute ()
        {
            var result = Client.GrpcGameServiceClient.GetBots(new());

            foreach (var bot in result.Types_)
                Console.Write($"{bot}; ");
            Console.WriteLine();
        }
    }
}
