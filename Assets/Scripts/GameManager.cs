using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MorBreaker
{
    /// <summary>
    /// Drives morBreaker: in-memory score, lives, and the 10-level progression,
    /// plus the win / lose flow. Subscribes to ball and brick events and updates
    /// the HUD. Game state lives only in memory. The single, deliberate exception
    /// to "store nothing" is the optional end-of-game leaderboard: the player may
    /// voluntarily type a nickname that, with the final score, is saved to a
    /// LOCAL high-score list via <see cref="Leaderboard"/> (browser localStorage
    /// in WebGL, in-memory otherwise) — never sent anywhere. Nothing else is
    /// collected or transmitted.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        private enum State { Ready, Playing, Ended }
        private enum EndPhase { NameEntry, Results }

        [Header("Scene references")]
        [SerializeField] private BallController ball;
        [SerializeField] private BrickSpawner spawner;

        [Header("HUD (optional)")]
        [SerializeField] private Text scoreText;
        [SerializeField] private Text livesText;
        [SerializeField] private Text messageText;
        [SerializeField] private Text versionText;

        [Header("Leaderboard (optional)")]
        [Tooltip("Score client. If unassigned or disabled, the name prompt and F1 panel are skipped.")]
        [SerializeField] private Leaderboard leaderboard;
        [Tooltip("Root panel holding the nickname input + submit button.")]
        [SerializeField] private GameObject nameEntryPanel;
        [SerializeField] private InputField nameInput;
        [SerializeField] private Button nameSubmitButton;
        [Tooltip("Root panel holding the top-scores list.")]
        [SerializeField] private GameObject leaderboardPanel;
        [SerializeField] private Text leaderboardText;
        [Tooltip("How many top entries to display.")]
        [SerializeField] private int topCount = 10;

        [Header("Rules")]
        [Tooltip("Lives the player starts each game with.")]
        [SerializeField] private int startingLives = 3;

        [Tooltip("Flat bonus for clearing the final level.")]
        [SerializeField] private int winBonusFlat = 1000;

        [Tooltip("Extra win bonus per remaining life.")]
        [SerializeField] private int winBonusPerLife = 500;

        private int _score;
        private int _lives;
        private int _level;
        private float _baseSpeed;
        private State _state;
        private EndPhase _endPhase;
        private bool _won;

        private void OnEnable()
        {
            BrickController.Destroyed += OnBrickDestroyed;
            BrickSpawner.LevelCleared += OnLevelCleared;
            BallController.Launched += OnBallLaunched;
            BallController.Lost += OnBallLost;
        }

        private void OnDisable()
        {
            BrickController.Destroyed -= OnBrickDestroyed;
            BrickSpawner.LevelCleared -= OnLevelCleared;
            BallController.Launched -= OnBallLaunched;
            BallController.Lost -= OnBallLost;
        }

        private void Start()
        {
            if (ball != null) _baseSpeed = ball.Speed;
            if (nameInput != null) nameInput.characterLimit = Leaderboard.MaxNameLength;
            if (nameSubmitButton != null) nameSubmitButton.onClick.AddListener(OnNameSubmitted);
            if (nameInput != null) nameInput.onSubmit.AddListener(_ => OnNameSubmitted());
            if (versionText != null) versionText.text = "v" + Application.version;
            NewGame();
        }

        private void Update()
        {
            // F1 toggles the top-scores panel at any time (except mid name-entry).
            if (TogglePressed()) ToggleLeaderboard();

            // After the results are shown, a tap / click / space starts a fresh game.
            if (_state == State.Ended && _endPhase == EndPhase.Results && RestartPressed())
                NewGame();
        }

        private void NewGame()
        {
            _score = 0;
            _lives = startingLives;
            _won = false;
            HideEndUi();

            UpdateScoreHud();
            UpdateLivesHud();
            StartLevel(1, "Tap to launch");
        }

        /// <summary>Configure the ball + bricks for a level and arm for launch.</summary>
        private void StartLevel(int level, string message)
        {
            _level = Mathf.Clamp(level, 1, LevelTable.Count);
            _state = State.Ready;

            if (ball != null)
            {
                ball.ConfigureLevel(_baseSpeed, LevelTable.AccelPerHit(_level));
                ball.Arm();
            }
            if (spawner != null) spawner.Build(LevelTable.Get(_level));

            ShowMessage(message);
        }

        private void OnBrickDestroyed(int points)
        {
            _score += points;
            UpdateScoreHud();
        }

        private void OnBallLaunched()
        {
            if (_state == State.Ready)
            {
                _state = State.Playing;
                ShowMessage(string.Empty);
            }
        }

        private void OnBallLost()
        {
            if (_state == State.Ended) return;

            _lives = Mathf.Max(0, _lives - 1);
            UpdateLivesHud();

            if (_lives <= 0)
                EndGame(won: false);
            else
            {
                _state = State.Ready;
                ShowMessage("Tap to launch");
            }
        }

        private void OnLevelCleared()
        {
            if (LevelTable.IsFinal(_level))
            {
                int bonus = winBonusFlat + winBonusPerLife * _lives;
                _score += bonus;
                UpdateScoreHud();
                EndGame(won: true);
            }
            else
            {
                StartLevel(_level + 1, $"Level {_level + 1}\nTap to launch");
            }
        }

        /// <summary>End the game (win or lose): freeze the ball, then prompt for a name or show results.</summary>
        private void EndGame(bool won)
        {
            _state = State.Ended;
            _won = won;
            if (ball != null) ball.Hold();

            string head = won ? "You Win!" : "Game Over";

            if (leaderboard != null && leaderboard.IsActive)
            {
                _endPhase = EndPhase.NameEntry;
                ShowMessage($"{head}\nScore {_score}\nEnter your name");
                ShowNameEntry();
            }
            else
            {
                _endPhase = EndPhase.Results;
                ShowMessage($"{head}\nScore {_score}\nTap to restart");
            }
        }

        private void OnNameSubmitted()
        {
            if (_state != State.Ended || _endPhase != EndPhase.NameEntry) return;

            string raw = nameInput != null ? nameInput.text : string.Empty;
            string name = Leaderboard.SanitizeName(raw);

            if (nameEntryPanel != null) nameEntryPanel.SetActive(false);
            _endPhase = EndPhase.Results;
            string head = _won ? "You Win!" : "Game Over";
            ShowMessage($"{head}\nScore {_score}\nSaving...");

            if (leaderboard != null)
                leaderboard.Submit(name, _score, _level, _won, _ =>
                    leaderboard.Fetch(topCount, entries =>
                    {
                        PopulateLeaderboard(entries);
                        ShowMessage($"{head}\nScore {_score}\nTap to restart");
                    }));
        }

        /// <summary>F1: open the panel (fetching fresh scores) or close it.</summary>
        private void ToggleLeaderboard()
        {
            if (leaderboard == null || !leaderboard.IsActive || leaderboardPanel == null) return;
            // Don't fight the name-entry step.
            if (_state == State.Ended && _endPhase == EndPhase.NameEntry) return;

            if (leaderboardPanel.activeSelf)
                leaderboardPanel.SetActive(false);
            else
                leaderboard.Fetch(topCount, PopulateLeaderboard);
        }

        private void PopulateLeaderboard(Leaderboard.Entry[] entries)
        {
            if (leaderboardText != null)
            {
                var sb = new StringBuilder();
                sb.AppendLine("TOP SCORES");
                sb.AppendLine();
                if (entries == null || entries.Length == 0)
                    sb.AppendLine("(no scores yet)");
                else
                {
                    int rank = 1;
                    foreach (var e in entries)
                    {
                        string flag = e.completed ? "WIN" : "   ";
                        sb.AppendLine($"{rank,2}. {e.name,-10} {e.score,6}  L{e.level,-2} {flag} {e.date}");
                        rank++;
                    }
                }
                sb.AppendLine();
                sb.AppendLine("F1 to close");
                leaderboardText.text = sb.ToString();
            }
            if (leaderboardPanel != null) leaderboardPanel.SetActive(true);
        }

        private void ShowNameEntry()
        {
            if (nameEntryPanel != null) nameEntryPanel.SetActive(true);
            if (leaderboardPanel != null) leaderboardPanel.SetActive(false);
            if (nameInput != null)
            {
                nameInput.text = string.Empty;
                nameInput.Select();
                nameInput.ActivateInputField();
            }
        }

        private void HideEndUi()
        {
            if (nameEntryPanel != null) nameEntryPanel.SetActive(false);
            if (leaderboardPanel != null) leaderboardPanel.SetActive(false);
        }

        private static bool TogglePressed()
        {
            var k = Keyboard.current;
            return k != null && k.f1Key.wasPressedThisFrame;
        }

        private static bool RestartPressed()
        {
            var pointer = Pointer.current;
            if (pointer != null && pointer.press.wasPressedThisFrame) return true;

            var k = Keyboard.current;
            if (k != null && (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame)) return true;

            return false;
        }

        private void UpdateScoreHud()
        {
            if (scoreText != null) scoreText.text = $"SCORE {_score}";
        }

        private void UpdateLivesHud()
        {
            if (livesText != null) livesText.text = $"LIVES {_lives}";
        }

        private void ShowMessage(string msg)
        {
            if (messageText != null) messageText.text = msg;
        }
    }
}
