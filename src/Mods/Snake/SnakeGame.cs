using System;
using System.Collections.Generic;
using System.Reflection;
using GorillaNetworking;
using UnityEngine;

namespace CascadeMenu.Mods
{
    /// <summary>
    /// Reflection bridge to the computer's Snake minigame. Everything in
    /// GorillaComputer is private, so members are resolved once and cached.
    /// A missing member leaves <see cref="Ready"/> false instead of throwing,
    /// which keeps the mods inert on game builds without the game.
    /// </summary>
    internal static class SnakeGame
    {
        private const int FallbackColumns = 42;

        private static bool _resolved;
        private static bool _ready;

        private static FieldInfo _body;
        private static FieldInfo _pellet;
        private static FieldInfo _direction;
        private static FieldInfo _lastDirection;
        private static FieldInfo _state;
        private static FieldInfo _rows;
        private static FieldInfo _score;
        private static FieldInfo _best;
        private static FieldInfo _columnsField;

        private static MethodInfo _switchState;
        private static MethodInfo _reset;
        private static MethodInfo _turn;
        private static MethodInfo _end;
        private static MethodInfo _updateScreen;

        private static int _columns = FallbackColumns;
        private static float _suppressRestartUntil;

        internal static bool Ready
        {
            get
            {
                Resolve();
                return _ready;
            }
        }

        internal static int Columns
        {
            get
            {
                Resolve();
                return _columns;
            }
        }

        internal static GorillaComputer Computer
        {
            get { return GorillaComputer.instance; }
        }

        internal static bool InSnake
        {
            get
            {
                GorillaComputer computer = Computer;
                return computer != null && computer.currentState == GorillaComputer.ComputerState.Snake;
            }
        }

        internal static string StateName
        {
            get
            {
                GorillaComputer computer = Computer;
                if (computer == null || _state == null) return string.Empty;
                try { return _state.GetValue(computer)?.ToString() ?? string.Empty; }
                catch { return string.Empty; }
            }
        }

        internal static bool IsPlaying
        {
            get
            {
                string state = StateName;
                return state == "Playing" || state == "Waiting";
            }
        }

        internal static bool IsGameOver
        {
            get { return StateName == "GameOver"; }
        }

        internal static List<Vector2Int> ReadBody()
        {
            GorillaComputer computer = Computer;
            if (computer == null || _body == null) return null;
            try { return (List<Vector2Int>)_body.GetValue(computer); }
            catch { return null; }
        }

        internal static Vector2Int ReadPellet()
        {
            return ReadVector(_pellet);
        }

        internal static Vector2Int ReadDirection()
        {
            return ReadVector(_direction);
        }

        internal static Vector2Int ReadLastDirection()
        {
            return ReadVector(_lastDirection);
        }

        internal static int ReadRows()
        {
            GorillaComputer computer = Computer;
            if (computer == null || _rows == null) return 0;
            try { return (int)_rows.GetValue(computer); }
            catch { return 0; }
        }

        internal static int ReadScore()
        {
            return ReadInt(_score);
        }

        internal static int ReadBest()
        {
            return ReadInt(_best);
        }

        internal static void Turn(Vector2Int direction)
        {
            GorillaComputer computer = Computer;
            if (computer == null || _turn == null) return;
            if (direction == Vector2Int.zero) return;

            try { _turn.Invoke(computer, new object[] { direction }); }
            catch (Exception exception) { Plugin.Log?.LogError("Cascade snake turn failed: " + exception.Message); }
        }

        internal static void Reset()
        {
            GorillaComputer computer = Computer;
            if (computer == null || _reset == null) return;

            try
            {
                _reset.Invoke(computer, null);
                if (_updateScreen != null) _updateScreen.Invoke(computer, null);
            }
            catch (Exception exception) { Plugin.Log?.LogError("Cascade snake reset failed: " + exception.Message); }
        }

        internal static void Open()
        {
            GorillaComputer computer = Computer;
            if (computer == null || _switchState == null) return;

            try
            {
                _switchState.Invoke(computer, new object[]
                {
                    GorillaComputer.ComputerState.Snake, true
                });
                if (_reset != null) _reset.Invoke(computer, null);
                if (_updateScreen != null) _updateScreen.Invoke(computer, null);
            }
            catch (Exception exception) { Plugin.Log?.LogError("Cascade snake open failed: " + exception.Message); }
        }

        internal static void Exit()
        {
            GorillaComputer computer = Computer;
            if (computer == null) return;

            try
            {
                computer.PressButton(GorillaKeyboardBindings.delete);
                if (_updateScreen != null) _updateScreen.Invoke(computer, null);
            }
            catch (Exception exception) { Plugin.Log?.LogError("Cascade snake exit failed: " + exception.Message); }
        }

        internal static void PressRestart()
        {
            GorillaComputer computer = Computer;
            if (computer == null) return;

            try { computer.PressButton(GorillaKeyboardBindings.enter); }
            catch (Exception exception) { Plugin.Log?.LogError("Cascade snake restart failed: " + exception.Message); }
        }

        internal static void End(bool newBest)
        {
            GorillaComputer computer = Computer;
            if (computer == null || _end == null) return;

            try
            {
                _end.Invoke(computer, new object[] { newBest });
                if (_updateScreen != null) _updateScreen.Invoke(computer, null);
            }
            catch (Exception exception) { Plugin.Log?.LogError("Cascade snake end failed: " + exception.Message); }
        }

        /// <summary>
        /// Sets the best score to a custom value.
        /// Returns false if the field is not accessible.
        /// </summary>
        internal static bool SetBestScore(int score)
        {
            GorillaComputer computer = Computer;
            if (computer == null || _best == null) return false;

            try
            {
                _best.SetValue(computer, Mathf.Max(0, score));
                if (_updateScreen != null) _updateScreen.Invoke(computer, null);
                return true;
            }
            catch (Exception exception)
            {
                Plugin.Log?.LogError("Cascade snake set best score failed: " + exception.Message);
                return false;
            }
        }

        /// <summary>
        /// Moves the pellet to a specific position.
        /// Returns false if the field is not accessible or position is invalid.
        /// </summary>
        internal static bool SetPelletPosition(Vector2Int position)
        {
            GorillaComputer computer = Computer;
            if (computer == null || _pellet == null) return false;

            int rows = ReadRows();
            if (position.x < 0 || position.x >= _columns || position.y < 0 || position.y >= rows)
                return false;

            // Don't place pellet on snake body
            List<Vector2Int> body = ReadBody();
            if (body != null && body.Contains(position))
                return false;

            try
            {
                _pellet.SetValue(computer, position);
                if (_updateScreen != null) _updateScreen.Invoke(computer, null);
                return true;
            }
            catch (Exception exception)
            {
                Plugin.Log?.LogError("Cascade snake set pellet failed: " + exception.Message);
                return false;
            }
        }

        /// <summary>
        /// Fills the body with every cell so the run ends at the maximum score.
        /// Returns false when the game is not actually running.
        /// </summary>
        internal static bool FillBoardForWin()
        {
            GorillaComputer computer = Computer;
            if (computer == null) return false;
            if (!InSnake || !IsPlaying) return false;

            List<Vector2Int> body = ReadBody();
            int rows = ReadRows();
            if (body == null || rows <= 0) return false;

            try
            {
                body.Clear();
                for (int y = 0; y < rows; y++)
                {
                    for (int x = 0; x < _columns; x++)
                    {
                        body.Add(new Vector2Int(x, y));
                    }
                }

                if (_score != null) _score.SetValue(computer, body.Count - 1);
                if (_updateScreen != null) _updateScreen.Invoke(computer, null);
                return true;
            }
            catch (Exception exception)
            {
                Plugin.Log?.LogError("Cascade snake instant win failed: " + exception.Message);
                return false;
            }
        }

        /// <summary>Stops auto restart from re-entering a game right after a win.</summary>
        internal static void SuppressRestart(float seconds)
        {
            _suppressRestartUntil = Time.time + Mathf.Max(0f, seconds);
        }

        internal static bool RestartSuppressed
        {
            get { return Time.time < _suppressRestartUntil; }
        }

        private static FieldInfo FindField(Type type, string name)
        {
            return type.GetField(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        }

        private static MethodInfo FindMethod(Type type, string name)
        {
            return type.GetMethod(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        }

        private static int ReadInt(FieldInfo field)
        {
            GorillaComputer computer = Computer;
            if (computer == null || field == null) return 0;
            try { return (int)field.GetValue(computer); }
            catch { return 0; }
        }

        private static Vector2Int ReadVector(FieldInfo field)
        {
            GorillaComputer computer = Computer;
            if (computer == null || field == null) return Vector2Int.zero;
            try { return (Vector2Int)field.GetValue(computer); }
            catch { return Vector2Int.zero; }
        }

        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;

            try
            {
                Type type = typeof(GorillaComputer);
                _body = FindField(type, "snakeBody");
                _pellet = FindField(type, "snakePellet");
                _direction = FindField(type, "snakeDirection");
                _lastDirection = FindField(type, "snakeLastStepDirection");
                _state = FindField(type, "snakeGameState");
                _rows = FindField(type, "snakeRows");
                _score = FindField(type, "snakeScore");
                _best = FindField(type, "snakeBestScore");
                _columnsField = FindField(type, "SnakeColumns");

                _switchState = FindMethod(type, "SwitchState");
                _reset = FindMethod(type, "ResetSnakeGame");
                _turn = FindMethod(type, "TurnSnake");
                _end = FindMethod(type, "EndSnakeGame");
                _updateScreen = FindMethod(type, "UpdateScreen");

                if (_columnsField != null)
                {
                    object value = _columnsField.GetValue(null);
                    if (value is int columns && columns > 0) _columns = columns;
                }

                _ready = _body != null && _pellet != null && _direction != null &&
                         _lastDirection != null && _state != null && _rows != null &&
                         _score != null && _turn != null && _end != null &&
                         _switchState != null && _reset != null && _updateScreen != null;
            }
            catch (Exception exception)
            {
                _ready = false;
                Plugin.Log?.LogWarning("Cascade snake bridge could not be resolved: " + exception.Message);
            }

            if (!_ready)
            {
                Plugin.Log?.LogWarning("Cascade snake: this game build has no Snake minigame; Snake mods stay disabled.");
            }
        }
    }

    /// <summary>
    /// Grid AI adapted from the standalone Snake plugin: it plans a safe BFS
    /// path to the pellet, verifies the whole path keeps the tail reachable,
    /// and falls back to the move that leaves the most open space.
    /// 
    /// OPTIMIZATIONS:
    /// - Cached grid state to avoid redundant calculations
    /// - Early exit conditions in pathfinding
    /// - Reusable collections to reduce GC pressure
    /// </summary>
    internal sealed class SnakeBrain
    {
        private static readonly Vector2Int[] Dirs =
        {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };

        private int _columns = 42;
        private float _desperationSeconds = 15f;

        private Vector2Int _lastHead = new Vector2Int(int.MinValue, int.MinValue);
        private Vector2Int _planned;
        private int _lastScore = -1;
        private float _survivalStart = -1f;

        // Reusable collections to reduce GC
        private readonly HashSet<Vector2Int> _blockedCache = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> _seenCache = new HashSet<Vector2Int>();
        private readonly Queue<Vector2Int> _queueCache = new Queue<Vector2Int>();
        private readonly Dictionary<Vector2Int, Vector2Int> _previousCache = new Dictionary<Vector2Int, Vector2Int>();
        private readonly List<Vector2Int> _pathCache = new List<Vector2Int>();
        private readonly Stack<Vector2Int> _stackCache = new Stack<Vector2Int>();

        internal float DesperationSeconds
        {
            get { return _desperationSeconds; }
            set { _desperationSeconds = Mathf.Max(1f, value); }
        }

        internal void Reset()
        {
            _lastHead = new Vector2Int(int.MinValue, int.MinValue);
            _planned = Vector2Int.zero;
            _lastScore = -1;
            _survivalStart = -1f;
            
            // Clear caches
            _blockedCache.Clear();
            _seenCache.Clear();
            _queueCache.Clear();
            _previousCache.Clear();
            _pathCache.Clear();
            _stackCache.Clear();
        }

        internal void Steer(GorillaComputer computer, List<Vector2Int> body, int rows)
        {
            if (computer == null || body == null || body.Count == 0) return;

            _columns = SnakeGame.Columns;
            if (rows <= 0) rows = 20;

            int score = SnakeGame.ReadScore();
            if (score != _lastScore)
            {
                _survivalStart = -1f;
                _lastScore = score;
            }

            Vector2Int head = body[0];
            Vector2Int pellet = SnakeGame.ReadPellet();
            Vector2Int currentDir = SnakeGame.ReadDirection();
            Vector2Int lastDir = SnakeGame.ReadLastDirection();

            // Only recalculate if head moved
            if (head != _lastHead)
            {
                _lastHead = head;
                _planned = Choose(body, pellet, lastDir, rows);
            }

            if (_planned != Vector2Int.zero && _planned != currentDir)
            {
                SnakeGame.Turn(_planned);
            }
        }

        private Vector2Int Choose(List<Vector2Int> body, Vector2Int pellet, Vector2Int lastDir, int rows)
        {
            List<Vector2Int> path = BfsPath(body, pellet, lastDir, rows);

            if (path != null && path.Count > 0 && IsPathSafe(body, path, pellet, rows))
            {
                _survivalStart = -1f;
                return FirstStep(body[0], path[0], lastDir);
            }

            if (_survivalStart < 0f) _survivalStart = Time.time;
            if (path != null && path.Count > 0 && Time.time - _survivalStart > _desperationSeconds)
            {
                _survivalStart = -1f;
                Vector2Int risky = FirstStep(body[0], path[0], lastDir);
                if (risky != Vector2Int.zero) return risky;
            }

            return Survival(body, pellet, lastDir, rows);
        }

        private static Vector2Int FirstStep(Vector2Int head, Vector2Int first, Vector2Int lastDir)
        {
            Vector2Int delta = first - head;
            if (lastDir != Vector2Int.zero && delta == -lastDir) return Vector2Int.zero;
            return delta;
        }

        private List<Vector2Int> BfsPath(List<Vector2Int> body, Vector2Int pellet, Vector2Int lastDir, int rows)
        {
            Vector2Int head = body[0];
            if (pellet == head) 
            {
                _pathCache.Clear();
                return _pathCache;
            }

            // Reuse collections
            _blockedCache.Clear();
            _previousCache.Clear();
            _seenCache.Clear();
            _queueCache.Clear();

            // Build blocked set (exclude tail as it will move)
            for (int i = 0; i < body.Count - 1; i++) 
                _blockedCache.Add(body[i]);

            Vector2Int reverse = -lastDir;
            _seenCache.Add(head);
            _queueCache.Enqueue(head);
            bool found = false;

            while (_queueCache.Count > 0)
            {
                Vector2Int current = _queueCache.Dequeue();
                if (current == pellet)
                {
                    found = true;
                    break;
                }

                for (int i = 0; i < Dirs.Length; i++)
                {
                    Vector2Int direction = Dirs[i];
                    
                    // Skip reverse direction from head
                    if (current == head && lastDir != Vector2Int.zero && direction == reverse) 
                        continue;

                    Vector2Int next = current + direction;
                    
                    // Bounds check
                    if (next.x < 0 || next.x >= _columns || next.y < 0 || next.y >= rows) 
                        continue;
                    
                    // Skip if already seen or blocked
                    if (_seenCache.Contains(next) || _blockedCache.Contains(next)) 
                        continue;

                    _seenCache.Add(next);
                    _previousCache[next] = current;
                    _queueCache.Enqueue(next);
                }
            }

            if (!found) return null;

            // Reconstruct path
            _pathCache.Clear();
            Vector2Int node = pellet;
            while (node != head)
            {
                _pathCache.Add(node);
                node = _previousCache[node];
            }
            _pathCache.Reverse();
            return _pathCache;
        }

        private bool IsPathSafe(List<Vector2Int> body, List<Vector2Int> path, Vector2Int pellet, int rows)
        {
            var virtualBody = new List<Vector2Int>(body);
            for (int i = 0; i < path.Count; i++)
            {
                Vector2Int direction = path[i] - virtualBody[0];
                virtualBody = StepBody(virtualBody, direction, pellet);
                if (IsCollision(virtualBody, rows)) return false;
            }

            var obstacles = new HashSet<Vector2Int>();
            for (int i = 0; i < virtualBody.Count - 1; i++) obstacles.Add(virtualBody[i]);
            return CanReach(virtualBody[0], virtualBody[virtualBody.Count - 1], obstacles, rows);
        }

        private static List<Vector2Int> StepBody(List<Vector2Int> body, Vector2Int direction, Vector2Int pellet)
        {
            Vector2Int newHead = body[0] + direction;
            var result = new List<Vector2Int>(body.Count + 1) { newHead };
            bool eats = newHead == pellet;
            int keep = eats ? body.Count : body.Count - 1;
            for (int i = 0; i < keep; i++) result.Add(body[i]);
            return result;
        }

        private bool IsCollision(List<Vector2Int> body, int rows)
        {
            Vector2Int head = body[0];
            if (head.x < 0 || head.x >= _columns || head.y < 0 || head.y >= rows) return true;
            for (int i = 1; i < body.Count; i++)
            {
                if (body[i] == head) return true;
            }
            return false;
        }

        private Vector2Int Survival(List<Vector2Int> body, Vector2Int pellet, Vector2Int lastDir, int rows)
        {
            Vector2Int head = body[0];
            Vector2Int reverse = -lastDir;
            Vector2Int best = Vector2Int.zero;
            int bestSpace = -1;
            bool bestReach = false;

            for (int i = 0; i < Dirs.Length; i++)
            {
                Vector2Int direction = Dirs[i];
                if (lastDir != Vector2Int.zero && direction == reverse) continue;

                Vector2Int next = head + direction;
                if (next.x < 0 || next.x >= _columns || next.y < 0 || next.y >= rows) continue;

                List<Vector2Int> post = StepBody(body, direction, pellet);
                if (IsCollision(post, rows)) continue;

                var obstacles = new HashSet<Vector2Int>();
                for (int j = 1; j < post.Count - 1; j++) obstacles.Add(post[j]);

                int space = FloodFill(next, obstacles, rows);
                bool reach = CanReach(next, post[post.Count - 1], obstacles, rows);

                bool better =
                    best == Vector2Int.zero ||
                    (reach && !bestReach) ||
                    (reach == bestReach && space > bestSpace);

                if (better)
                {
                    best = direction;
                    bestSpace = space;
                    bestReach = reach;
                }
            }

            return best;
        }

        private int FloodFill(Vector2Int start, HashSet<Vector2Int> obstacles, int rows)
        {
            if (start.x < 0 || start.x >= _columns || start.y < 0 || start.y >= rows) return 0;
            if (obstacles.Contains(start)) return 0;

            // Reuse stack cache
            _seenCache.Clear();
            _stackCache.Clear();
            
            _seenCache.Add(start);
            _stackCache.Push(start);

            while (_stackCache.Count > 0)
            {
                Vector2Int current = _stackCache.Pop();
                for (int i = 0; i < Dirs.Length; i++)
                {
                    Vector2Int next = current + Dirs[i];
                    if (next.x < 0 || next.x >= _columns || next.y < 0 || next.y >= rows) continue;
                    if (obstacles.Contains(next)) continue;
                    if (!_seenCache.Add(next)) continue;
                    _stackCache.Push(next);
                }
            }

            return _seenCache.Count;
        }

        private bool CanReach(Vector2Int start, Vector2Int goal, HashSet<Vector2Int> obstacles, int rows)
        {
            if (start == goal) return true;

            // Reuse queue cache  
            _seenCache.Clear();
            _queueCache.Clear();
            
            _seenCache.Add(start);
            _queueCache.Enqueue(start);

            while (_queueCache.Count > 0)
            {
                Vector2Int current = _queueCache.Dequeue();
                for (int i = 0; i < Dirs.Length; i++)
                {
                    Vector2Int next = current + Dirs[i];
                    if (next.x < 0 || next.x >= _columns || next.y < 0 || next.y >= rows) continue;
                    if (obstacles.Contains(next)) continue;
                    if (!_seenCache.Add(next)) continue;
                    if (next == goal) return true;
                    _queueCache.Enqueue(next);
                }
            }

            return false;
        }
    }
}
