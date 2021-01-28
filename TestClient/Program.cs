#nullable enable

using System;

using Grpc.Core;

using MazeGame.GrpcService;

namespace GreeterClient
{
    internal class Program
    {
        public static void Main (string[] args)
        {
            Channel channel = new("127.0.0.1:30051", ChannelCredentials.Insecure);

            var client = new GrpcGameService.GrpcGameServiceClient(channel);

            var reply = client.LogIn(new AuthorizationData { UserData = new UserData { Login = "Ger1", PasswordHash = "1123" }, ClearActiveConnection = true });

            Console.WriteLine($"{reply.Status}, {reply.PlayerGuid}");

            var reply2 = client.ClosePlayerConnection(new MazeGame.GrpcService.Guid { Guid_ = reply.PlayerGuid.Guid_ });

            Console.WriteLine(reply2.RequestingGuidStatus);
        }
    }
}
