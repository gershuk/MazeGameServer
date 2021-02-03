using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MazeGame.Client
{
    class ShowClientStateCommand : ClientCommand
    {
        public override void Execute ()
        {
            Console.WriteLine($"Client state = {Client.State}");
            Console.WriteLine($"Created room guid = {Client.CreatedRoomGuid}");
            Console.WriteLine($"Current room guid = {Client.ConnectedRoomGuid}");
        }
    }
}
