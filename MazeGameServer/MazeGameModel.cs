#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using AsyncExtensions;

namespace MazeGame.Server
{
    public enum AvatarGameState
    {
        Running = 0,
        Win = 1,
        Lose = 2,
    }

    public record GameInfo (List<(Vector2Int position, BlockType type)> Blocks, List<(Vector2Int position, string name)> Players, Vector2Int Position, AvatarGameState AvatarGameState, uint Turn, Vector2Int MapSize);

    public enum Direction
    {
        None = 0,
        Up = 1,
        Down = 2,
        Left = 3,
        Right = 4
    }

    public enum BlockType
    {
        Undefined = 0,
        Wall = 1,
        Empty = 2,
        Exit = 3,
    }

    public class PlayerAvatar
    {
        private bool _disposed;
        private List<(Vector2Int pos, BlockType type)> _lastVisionData;
        private readonly uint _lastTurnNumber;
        private AvatarGameState _avatarGameState;

        public Vector2Int StartPosition { get; init; }
        public Vector2Int Position { get; set; }
        public Direction Direction { get; set; }
        public BlockType[,] VisibleBlocks { get; init; }
        public AsyncBuffer<GameInfo> InfoBuffer { get; private set; }
        public string Login { get; init; }

        ~PlayerAvatar () => Dispose(false);

        public PlayerAvatar (Vector2Int position, Vector2Int mapSize, string login, uint lastTurnNumber)
        {
            _disposed = false;
            _lastVisionData = new();
            _avatarGameState = AvatarGameState.Running;
            _lastTurnNumber = lastTurnNumber;
            Login = login;
            StartPosition = position;
            Position = position;
            Direction = Direction.None;
            VisibleBlocks = new BlockType[mapSize.X, mapSize.Y];
            InfoBuffer = new();
        }

        private void SetVision (GameMap map, Vector2Int exitCoord)
        {
            var dist = Math.Max(1, Math.Min(map.Size.X, map.Size.Y) / 20);
            List<(Vector2Int pos, BlockType type)> ans = new();
            for (var i = -dist; i <= dist; ++i)
            {
                for (var j = -dist; j <= dist; ++j)
                {
                    var pos = Position - new Vector2Int(i, j);
                    if (pos.X < 0 || pos.X > map.Size.X - 1 || pos.Y < 0 || pos.Y > map.Size.Y - 1)
                        continue;
                    if (VisibleBlocks[pos.X, pos.Y] == BlockType.Undefined)
                    {
                        VisibleBlocks[pos.X, pos.Y] = map.Walls[pos.X, pos.Y] ? BlockType.Wall : BlockType.Empty;
                        if (pos == exitCoord)
                            VisibleBlocks[pos.X, pos.Y] = BlockType.Exit;
                        ans.Add((pos, VisibleBlocks[pos.X, pos.Y]));
                    }
                }
            }
            _lastVisionData = ans;
        }

        public async Task<AvatarGameState> SendData (IImmutableDictionary<Guid, PlayerAvatar> players, IImmutableList<(string name, Bot botAvatar)> bots, GameMap map,
            Vector2Int exitCoord, uint turn, bool isFull = false)
        {
            SetVision(map, exitCoord);
            List<(Vector2Int position, string name)> enemys = new();
            foreach (var player in players)
            {
                if (VisibleBlocks[player.Value.Position.X, player.Value.Position.Y] != BlockType.Undefined)
                    enemys.Add((player.Value.Position, player.Value.Login));
            }

            foreach (var bot in bots)
            {
                if (VisibleBlocks[bot.botAvatar.Position.X, bot.botAvatar.Position.Y] != BlockType.Undefined)
                    enemys.Add((bot.botAvatar.Position, "Bot"));
            }

            if (turn - 1 == _lastTurnNumber)
                _avatarGameState = AvatarGameState.Lose;
            if (exitCoord == Position)
                _avatarGameState = AvatarGameState.Win;

            if (isFull)
            {
                _lastVisionData = new(map.Size.X * map.Size.X);
                for (var i = 0; i < map.Size.X; ++i)
                {
                    for (var j = 0; j < map.Size.Y; ++j)
                    {
                        _lastVisionData.Add((new Vector2Int(i, j), VisibleBlocks[i, j]));
                    }
                }
            }


            await InfoBuffer.AsyncWrite(new GameInfo(_lastVisionData, enemys, Position, _avatarGameState, turn, map.Size));
            return _avatarGameState;
        }

        public AsyncBuffer<GameInfo> CreateNewInfoBuffer ()
        {
            InfoBuffer.Dispose();
            return InfoBuffer = new();
        }

        protected virtual void Dispose (bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                InfoBuffer.Dispose();
            }

            _disposed = true;
        }

        public void Dispose ()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

    public class MazeGameModel
    {
        private bool _disposed;
        private readonly uint _turnTime;
        private readonly GameMap _map;
        private readonly ConcurrentDictionary<Guid, PlayerAvatar> _players;
        private readonly ConcurrentDictionary<Guid, AsyncBuffer<GameInfo>> _spectators;
        private readonly List<(string name, Bot botAvatar)> _bots;
        private readonly Vector2Int _exitCoords;
        private readonly CancellationTokenSource _tokenSource;
        private readonly CancellationToken _token;
        private uint _currentTurn;
        private Action<Guid> _deletePlayer;
        private Action _deleteRoom;

        public uint MaxTurns { get; init; }

        ~MazeGameModel () => Dispose(false);

        public MazeGameModel (Action DeleteRoom, Action<Guid> DeletePlayer, [NotNull] GameMap map, [NotNull] List<(Guid guid, string login)> players, [NotNull] List<(string name, Bot botAvatar)> bots, uint turns = 100, uint turnTime = 5000)
        {
            _disposed = false;
            _tokenSource = new CancellationTokenSource();
            _token = _tokenSource.Token;

            var rand = new Random();
            _turnTime = turnTime;
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _bots = bots ?? throw new ArgumentNullException(nameof(bots));
            _players = new();
            _spectators = new();
            var existIndex = rand.Next();
            existIndex %= _map.Exits.Length;
            _exitCoords = _map.Exits[existIndex];
            MaxTurns = turns;
            _currentTurn = 0;
            _deletePlayer = DeletePlayer;
            _deleteRoom = DeleteRoom;

            var positions = map.Spawns.OrderBy(x => rand.Next()).ToList();
            var i = 0;
            foreach (var (guid, login) in players)
            {
                PlayerAvatar playerAvatar = new(positions[i], _map.Size, login, turns - 1);
                _players.TryAdd(guid, playerAvatar);
                ++i;
                playerAvatar.SendData(_players.ToImmutableDictionary(), _bots.ToImmutableList(), _map, _exitCoords, 0).Wait();
            }

            foreach (var bot in bots)
            {
                bot.botAvatar.Init(_map, positions[i], _exitCoords);
                ++i;
            }
        }

        public async Task StartGame () => await Task.Run(GameLoop, _token).ContinueWith(EndGame, TaskContinuationOptions.OnlyOnCanceled);

        public async Task GameLoop ()
        {

            await Task.Delay((int) _turnTime);
            for (var i = 1; i <= MaxTurns; ++i)
            {
                _currentTurn = (uint) i;
                foreach (var player in _players)
                {
                    var pos = player.Value.Position + player.Value.Direction switch
                    {
                        Direction.None => new Vector2Int(0, 0),
                        Direction.Up => new Vector2Int(0, 1),
                        Direction.Down => new Vector2Int(0, -1),
                        Direction.Left => new Vector2Int(-1, 0),
                        Direction.Right => new Vector2Int(1, 0),
                        _ => throw new NotImplementedException(),
                    };

                    player.Value.Direction = Direction.None;

                    if (!(pos.X < 0 || pos.X > _map.Size.X - 1 || pos.Y < 0 || pos.Y > _map.Size.Y - 1))
                    {
                        if (!_map.Walls[pos.X, pos.Y])
                            player.Value.Position = pos;
                    }
                }

                List<(string name, Bot botAvatar)> endedBots = new();
                foreach (var bot in _bots)
                {
                    if (bot.botAvatar.Move())
                        endedBots.Add(bot);
                }

                List<Guid> endedPlayerGuid = new();
                foreach (var player in _players)
                {
                    var state = await player.Value.SendData(_players.ToImmutableDictionary(), _bots.ToImmutableList(), _map, _exitCoords, (uint) i);
                    if (state != AvatarGameState.Running)
                        endedPlayerGuid.Add(player.Key);
                }

                foreach (var buffer in _spectators)
                {
                    List<(Vector2Int pos, string login)> players = _players.Select(p => (p.Value.Position, p.Value.Login)).ToList();
                    players.AddRange(_bots.Select(b => (b.botAvatar.Position, b.name)));
                    var gameInfo = new GameInfo(new(), players, new(0, 0), AvatarGameState.Running, _currentTurn, _map.Size);
                    await buffer.Value.AsyncWrite(gameInfo);
                }

                await Task.Delay((int) _turnTime);

                foreach (var bot in endedBots)
                    _bots.Remove(bot);
                foreach (var guid in endedPlayerGuid)
                {
                    try
                    {
                        _deletePlayer(guid);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                }

                if (_bots.Count + _players.Count == 0)
                    break;
            }
            try
            {
                _deleteRoom();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public void SetPlayerDirection (Guid guid, Direction direction, uint turn)
        {
            if (_currentTurn != turn)
                throw new WrongTurnException();
            if (!_players.ContainsKey(guid))
                throw new PlayerNotFoundInGameExeception();

            _players[guid].Direction = direction;
        }

        public void EndGame (Task? task)
        {
            foreach (var player in _players)
            {
                try
                {
                    _deletePlayer(player.Key);
                }
                catch (Exception e)
                {

                    Console.WriteLine(e);
                }
            }

            foreach (var spectator in _spectators)
            {
                spectator.Value.Dispose();
                _spectators.Remove(spectator.Key, out _);
            }

            _bots.Clear();
            try
            {
                _deleteRoom();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public async Task<AsyncBuffer<GameInfo>> GetPlayerData (Guid guid)
        {
            if (!_players.TryGetValue(guid, out var playerAvatar))
                throw new PlayerNotFoundInGameExeception();
            var buffer = playerAvatar.CreateNewInfoBuffer();
            await playerAvatar.SendData(_players.ToImmutableDictionary(), _bots.ToImmutableList(), _map, _exitCoords, _currentTurn, true);
            return buffer;
        }

        public void DeletePlayer (Guid guid)
        {
            if (!_players.Remove(guid, out var playerAvatar))
                throw new PlayerNotFoundInGameExeception();
            playerAvatar.Dispose();
        }

        public async Task<AsyncBuffer<GameInfo>> AddSpectator (Guid guid, bool isForced)
        {
            if (isForced)
            {
                if (_spectators.TryRemove(guid, out var asyncBuffer))
                    asyncBuffer.Dispose();
            }
            else
            {
                if (_spectators.ContainsKey(guid))
                    throw new PlayerAlreadySpectedThisRoomException();
            }

            _spectators[guid] = new();
            List<(Vector2Int pos, BlockType type)> blocks = new(_map.Size.X * _map.Size.X);
            for (var i = 0; i < _map.Size.X; ++i)
            {
                for (var j = 0; j < _map.Size.Y; ++j)
                {
                    Vector2Int pos = new(i, j);
                    var blockType = BlockType.Empty;
                    if (_exitCoords == pos)
                        blockType = BlockType.Exit;
                    if (_map.Walls[i, j])
                        blockType = BlockType.Wall;
                    blocks.Add((pos, blockType));
                }
            }

            List<(Vector2Int pos, string login)> players = _players.Select(p => (p.Value.Position, p.Value.Login)).ToList();
            players.AddRange(_bots.Select(b => (b.botAvatar.Position, "Bot")));


            await _spectators[guid].AsyncWrite(new(blocks, players, new(0, 0), AvatarGameState.Running, _currentTurn, _map.Size));

            return _spectators[guid];
        }

        public void DeleteSpectator (Guid guid)
        {
            if (_spectators.Remove(guid, out var asyncBuffer))
            {
                asyncBuffer.Dispose();
            }
            else
            {
                throw new SpectatingChannelNotFoundException();
            }
        }

        protected virtual void Dispose (bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _tokenSource.Cancel();
                EndGame(null);
            }

            _disposed = true;
        }

        public void Dispose ()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
