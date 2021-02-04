#nullable enable

using System;

namespace MazeGame.Client
{
    internal class Program
    {
        public static void Main ()
        {
            Console.WriteLine("Enter ip");
            var ip = Console.ReadLine();
            if (ip == null || ip == string.Empty)
            {
                ip = "127.0.0.1";
                Console.WriteLine(ip);
            }
            Console.WriteLine("Enter port");
            var port = Console.ReadLine();
            if (port == null || port == string.Empty)
            {
                port = "30051";
                Console.WriteLine(port);
            }

            var client = new ConsoleClient(ip, Convert.ToUInt32(port));
            client.AddCommand<LogInCommand>("Login");
            client.AddCommand<CloseConnectionCommand>("Disconnect");
            client.AddCommand<RegisterPlayerCommand>("Register");
            client.AddCommand<GetBotsCommand>("GetBots");
            client.AddCommand<GetMapsCommand>("GetMaps");
            client.AddCommand<GetPlayerStateCommand>("GetMyState");
            client.AddCommand<ShowClientStateCommand>("ShowMyState");
            client.AddCommand<CreateRoomCommand>("CreateRoom");
            client.AddCommand<ConnectToRoomCommand>("ConnectToRoom");
            client.AddCommand<GetRoomsCommand>("GetRooms");
            client.AddCommand<StartGameCommand>("StartGame");
            client.AddCommand<SpectateGameCommand>("SpectateGame");
            client.AddCommand<KickCommand>("Kick");
            client.AddCommand<DeleteRoom>("DeleteRoom");
            client.AddCommand<StoppSpectateCommand>("StopSpectate");
            client.AddCommand<UpCommand>("W");
            client.AddCommand<DownCommand>("S");
            client.AddCommand<LeftCommand>("A");
            client.AddCommand<RightCommand>("D");

            client.ReadCommands();
        }
    }
}
