using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;

namespace MazeGame.Server
{
    public class PathNode
    {
        
        public Point Position { get; set; }
        
        public int PathLengthFromStart { get; set; }
        
        public PathNode CameFrom { get; set; }
        
        public int HeuristicEstimatePathLength { get; set; }
        
        public int EstimateFullPathLength => PathLengthFromStart + HeuristicEstimatePathLength;
    }

    public static class AStar
    {
        public static List<Point> FindPath (bool[,] field, Point start, Point goal)
        {
            
            var closedSet = new Collection<PathNode>();
            var openSet = new Collection<PathNode>();
            
            var startNode = new PathNode()
            {
                Position = start,
                CameFrom = null,
                PathLengthFromStart = 0,
                HeuristicEstimatePathLength = GetHeuristicPathLength(start, goal)
            };
            openSet.Add(startNode);
            while (openSet.Count > 0)
            {
                
                var currentNode = openSet.OrderBy(node =>
                  node.EstimateFullPathLength).First();
                
                if (currentNode.Position == goal)
                    return GetPathForNode(currentNode);
                
                openSet.Remove(currentNode);
                closedSet.Add(currentNode);
                
                foreach (var neighbourNode in GetNeighbours(currentNode, goal, field))
                {
                    
                    if (closedSet.Count(node => node.Position == neighbourNode.Position) > 0)
                        continue;
                    var openNode = openSet.FirstOrDefault(node =>
                      node.Position == neighbourNode.Position);
                    
                    if (openNode == null)
                    {
                        openSet.Add(neighbourNode);
                    }
                    else
                      if (openNode.PathLengthFromStart > neighbourNode.PathLengthFromStart)
                    {
                        
                        openNode.CameFrom = currentNode;
                        openNode.PathLengthFromStart = neighbourNode.PathLengthFromStart;
                    }
                }
            }
            
            return null;
        }

        private static Collection<PathNode> GetNeighbours (PathNode pathNode, Point goal, bool[,] field)
        {
            var result = new Collection<PathNode>();

            
            var neighbourPoints = new Point[4];
            neighbourPoints[0] = new Point(pathNode.Position.X + 1, pathNode.Position.Y);
            neighbourPoints[1] = new Point(pathNode.Position.X - 1, pathNode.Position.Y);
            neighbourPoints[2] = new Point(pathNode.Position.X, pathNode.Position.Y + 1);
            neighbourPoints[3] = new Point(pathNode.Position.X, pathNode.Position.Y - 1);

            foreach (var point in neighbourPoints)
            {
                
                if (point.X < 0 || point.X >= field.GetLength(0))
                    continue;
                if (point.Y < 0 || point.Y >= field.GetLength(1))
                    continue;
                
                if (field[point.X, point.Y])
                    continue;
                
                var neighbourNode = new PathNode()
                {
                    Position = point,
                    CameFrom = pathNode,
                    PathLengthFromStart = pathNode.PathLengthFromStart +
                    GetDistanceBetweenNeighbours(),
                    HeuristicEstimatePathLength = GetHeuristicPathLength(point, goal)
                };
                result.Add(neighbourNode);
            }
            return result;
        }

        private static int GetDistanceBetweenNeighbours () => 1;

        private static int GetHeuristicPathLength (Point from, Point to) => Math.Abs(from.X - to.X) + Math.Abs(from.Y - to.Y);

        private static List<Point> GetPathForNode (PathNode pathNode)
        {
            var result = new List<Point>();
            var currentNode = pathNode;
            while (currentNode != null)
            {
                result.Add(currentNode.Position);
                currentNode = currentNode.CameFrom;
            }
            result.Reverse();
            return result;
        }
    }

    internal class AStrarBot : Bot
    {
        private List<Point> _points;
        private int _i = 0;

        public override void Init (GameMap map, Vector2Int startPosition, Vector2Int endPoint)
        {
            Position = startPosition;
            _points = _points = AStar.FindPath(map.Walls, new Point(startPosition.X, startPosition.Y), new Point(endPoint.X, endPoint.Y));
        }

        public override bool Move ()
        {
            _i++;
            Position = new Vector2Int(_points[_i].X, _points[_i].Y);
            return _points.Count -1  == _i;
        }
    }
}
