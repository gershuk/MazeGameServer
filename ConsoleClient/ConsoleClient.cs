#nullable enable

using System;
using System.Collections.Generic;

using Grpc.Core;

using MazeGame.GrpcService;

namespace MazeGame.Client
{
    public enum ClientState
    {
        Started = 0,
        Authorized = 1,
        InLobby = 2,
        InGame = 3,
        Undefined = 4,
    }

    public abstract class ClientCommand
    {
        public ConsoleClient Client { get; init; }
        public abstract void Execute ();
    }

    public class ConsoleClient
    {
        private readonly Dictionary<string, ClientCommand> _commands;

        public ClientState State { get; set; }
        public System.Guid? CreatedRoomGuid { get; set; }
        public System.Guid? ConnectedRoomGuid { get; set; }
        public System.Guid? PlayerGuid { get; set; }
        public GrpcGameService.GrpcGameServiceClient GrpcGameServiceClient { get; init; }
        public UInt32 CurrentTurn { get; set; } = 0;

        public ConsoleClient (string ip = "127.0.0.1", uint port = 30051, params (string name, ClientCommand command)[] commands)
        {
            _commands = new();
            foreach (var (name, command) in commands)
                _commands.Add(name, command);
            GrpcGameServiceClient = new(new Channel($"{ip}:{port}", ChannelCredentials.Insecure));
            Console.WriteLine("Client created");
        }

        public void ClearData ()
        {
            CurrentTurn = 0;
            State = ClientState.Started;
            CreatedRoomGuid = null;
            ConnectedRoomGuid = null;
            PlayerGuid = null;
        }

        public void AddCommand<T> (string name) where T : ClientCommand, new() => _commands.Add(name, new T() { Client = this });

        public void RemoveCommand (string name) => _commands.Remove(name);

        public void ExecuteCommand (string name) => _commands[name].Execute();

        public void WriteCommandList ()
        {
            foreach (var command in _commands)
                Console.Write($"{command.Key}; ");
            Console.WriteLine();
        }

        public void ReadCommands ()
        {
            while (true)
            {
                var commandName = Console.ReadLine();
                if (commandName.Equals("EXIT", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Shutdown client");
                    break;
                }

                if (commandName.Equals("HELP", StringComparison.OrdinalIgnoreCase))
                {
                    WriteCommandList();
                    continue;
                }

                if (commandName != null && _commands.TryGetValue(commandName, out var clientCommand))
                {
                    clientCommand.Execute();
                }
                else
                {
                    Console.WriteLine("Wrong command");
                    WriteCommandList();
                }
            }
        }

        public void SyncState()
        {
            var syncInfo = GrpcGameServiceClient.GetPlayerState(new() { Guid_ = PlayerGuid.Value.ToString("D") });
            State = syncInfo.PlayerState switch
            {
                PlayerState.Authorized => ClientState.Authorized,
                PlayerState.InLobby => ClientState.InLobby,
                PlayerState.InGame => ClientState.InGame,
                PlayerState.Undefined => ClientState.Undefined,
            };
            CreatedRoomGuid = new(syncInfo.CreatedRoomGuid.Guid_);
            ConnectedRoomGuid = new(syncInfo.ConnectToRoomGuid.Guid_);
        }

        public T GetCommandByType<T>() where T : ClientCommand
        {
            foreach (var command in _commands)
                if (command.Value is T obj)
                    return obj;
            return null;
        }    
    }
}
