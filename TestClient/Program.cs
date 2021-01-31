#nullable enable

using System;
using System.Threading.Tasks;

using Grpc.Core;

using MazeGame.GrpcService;

namespace GreeterClient
{
    internal class Program
    {
        public static void Main ()
        {
            Channel channel = new("127.0.0.1:30051", ChannelCredentials.Insecure);

            var client = new GrpcGameService.GrpcGameServiceClient(channel);

            var reply = client.LogIn(new AuthorizationData { UserData = new UserData { Login = "Ger1", PasswordHash = "1123" }, ClearActiveConnection = true });

            Console.WriteLine($"{reply.Status}, {reply.PlayerGuid}");

            var b = client.GetBots(new Empty());

            foreach (var info in b.Types_)
                Console.WriteLine(info);

            var m = client.GetMaps(new Empty());

            foreach (var info in m.MapInfos)
                Console.WriteLine(info);

            OwnerRoomConfiguration roomConfig = new()
            {
                OwnerGuid = reply.PlayerGuid,
                Properties = new()
                {
                    Name = "TestN",
                    Description = "TestD",
                    HasPassword = false,
                    MaxPlayerCount = 1,
                    MapGuid = m.MapInfos[0].Guid,
                }
            };

            var r = client.CreateRoom(roomConfig);

            Console.WriteLine(r.RequestingGuidStatus);
            Console.WriteLine(r.RoomGuid);
            Console.WriteLine(r.Status);

            PlayerAndRoomGuids playerAndRoomGuids = new()
            {
                Password = "",
                PlayerGuid = reply.PlayerGuid,
                RoomGuid = r.RoomGuid,
            };

            var t = Test(client, playerAndRoomGuids);
            Console.ReadKey();
            {
                var reply2 = client.ClosePlayerConnection(new MazeGame.GrpcService.Guid { Guid_ = reply.PlayerGuid.Guid_ });
                Console.WriteLine(reply2.RequestingGuidStatus);
            }
            t.Wait();
        }

        private static async Task Test (GrpcGameService.GrpcGameServiceClient client, PlayerAndRoomGuids playerAndRoomGuids)
        {
            var c = client.ConnectToRoom(playerAndRoomGuids);
            //while (true)
            {
                var t = await c.ResponseStream.MoveNext();

                Console.WriteLine(c.ResponseStream.Current);
            }
        }
    }
}
