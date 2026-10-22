using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace com.github.lhervier.ksp.diag.resourcetransfer
{
    /// <summary>
    /// In flight, shows how the part action windows pair their resource transfer controls: a letter above each
    /// open window, a badge on each transfer control with the letters of the windows of its targets, and a
    /// window that lists every control with its targets and keeps a journal of what changes. Reads only: nothing
    /// in the game is changed.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class KSPDiagResourceTransfer : MonoBehaviour
    {
        private const string LOG_PREFIX = "[KSPDiagResourceTransfer] ";

        private const int WINDOW_ID = 0x47485007;
        private const float WINDOW_WIDTH = 400f;
        private const float WINDOW_MARGIN = 60f;
        private const float LIST_HEIGHT = 150f;
        private const float JOURNAL_HEIGHT = 110f;

        /// <summary>How many target letters a badge writes before it writes how many more there are.</summary>
        private const int BADGE_LETTERS = 5;

        // Where a badge sits in its transfer row: past the row's label, before its buttons.
        private const float BADGE_LEFT = 46f;
        private const float BADGE_HEIGHT = 16f;

        private readonly Survey survey = new Survey();
        private Rect windowRect;
        private Vector2 listScroll;
        private Vector2 journalScroll;

        private GUIStyle badgeStyle;
        private GUIStyle letterStyle;
        private GUIStyle smallStyle;

        private void Awake()
        {
            windowRect = new Rect(Screen.width - WINDOW_WIDTH - WINDOW_MARGIN, WINDOW_MARGIN, WINDOW_WIDTH, 0f);
        }

        // Mod+F6 shows or hides the window, the same key for every KSP Diag. Static: the choice holds from one
        // flight scene to the next.
        private static readonly KeyBinding WINDOW_KEY = new KeyBinding(KeyCode.F6);
        private static bool windowVisible = true;

        /// <summary>Whether the window shows, as Mod+F6 toggles it; the badges and the journal go on either way.</summary>
        internal static bool WindowVisible
        {
            get { return windowVisible; }
            set { windowVisible = value; }
        }

        /// <summary>Where the window is on the screen, and how big.</summary>
        internal Rect WindowRect
        {
            get { return windowRect; }
            set { windowRect = value; }
        }

        /// <summary>What the survey read on the last frame.</summary>
        internal Survey Survey => survey;

        private void Update()
        {
            if (GameSettings.MODIFIER_KEY.GetKey() && WINDOW_KEY.GetKeyDown())
            {
                windowVisible = !windowVisible;
            }
        }

        private void LateUpdate()
        {
            // After every Update of the frame: the windows have built and updated their controls.
            survey.Read(Time.frameCount);
        }

        private void OnGUI()
        {
            GUI.skin = HighLogic.Skin;
            MakeStyles();
            DrawBadges();
            if (windowVisible)
            {
                windowRect = GUILayout.Window(WINDOW_ID, windowRect, DrawWindow, "KSP Diag - Resource Transfer");
            }
        }

        /// <summary>
        /// Writes to KSP.log what the window shows, every control with its targets, then the whole journal.
        /// Returns how many lines it wrote.
        /// </summary>
        internal int WriteLog()
        {
            List<string> lines = new List<string>();
            lines.Add($"frame {Time.frameCount}, {survey.Windows.Count} window(s), {survey.Controls.Count} transfer control(s)");
            foreach (WindowReading w in survey.Windows)
            {
                lines.Add($"window {w.Letter} {w.Part} {WindowState(w)}");
            }
            foreach (ControlReading c in survey.Controls)
            {
                lines.Add($"{ControlLine(c)}; {TargetsLine(c)}");
            }
            lines.Add($"journal, {survey.Journal.Count} line(s):");
            lines.AddRange(survey.Journal);
            foreach (string line in lines)
            {
                Debug.Log(LOG_PREFIX + line);
            }
            return lines.Count;
        }

        private void MakeStyles()
        {
            if (badgeStyle != null)
            {
                return;
            }
            Texture2D dark = new Texture2D(1, 1);
            dark.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.8f));
            dark.Apply();
            badgeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
                padding = new RectOffset(3, 3, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };
            badgeStyle.normal.background = dark;
            badgeStyle.normal.textColor = Color.white;
            letterStyle = new GUIStyle(badgeStyle)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = false,
                margin = new RectOffset(4, 4, 0, 0),
                padding = new RectOffset(0, 0, 0, 0)
            };
        }

        /// <summary>Draws the letter of each open window above it, and a badge on each of its transfer controls.</summary>
        private void DrawBadges()
        {
            foreach (WindowReading w in survey.Windows)
            {
                if (!w.Open)
                {
                    continue;
                }
                Rect r = ScreenRect((RectTransform)w.Window.transform);
                GUI.Label(new Rect(r.x, r.y - 20f, 24f, 18f), w.Letter, letterStyle);
            }
            foreach (ControlReading c in survey.Controls)
            {
                if (!c.WindowOpen || c.Control == null || !c.Control.gameObject.activeInHierarchy)
                {
                    continue;
                }
                Rect row = ScreenRect((RectTransform)c.Control.transform);
                string text = "→ " + Survey.TargetLetters(c, BADGE_LETTERS);
                if (c.State != "None")
                {
                    text += " · " + c.State;
                }
                Vector2 size = badgeStyle.CalcSize(new GUIContent(text));
                GUI.Label(new Rect(row.x + BADGE_LEFT, row.y + (row.height - BADGE_HEIGHT) / 2f, size.x, BADGE_HEIGHT),
                    text, badgeStyle);
            }
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginVertical();
            GUILayout.Label($"Frame {Time.frameCount}");
            GUILayout.Label("Part action windows:");
            if (survey.Windows.Count == 0)
            {
                GUILayout.Label("  none", smallStyle);
            }
            foreach (WindowReading w in survey.Windows)
            {
                GUILayout.Label($"  {w.Letter}  {w.Part}, {WindowState(w)}", smallStyle);
            }

            GUILayout.Label("Transfer controls:");
            listScroll = GUILayout.BeginScrollView(listScroll, GUILayout.Height(LIST_HEIGHT));
            if (survey.Controls.Count == 0)
            {
                GUILayout.Label("  none", smallStyle);
            }
            foreach (ControlReading c in survey.Controls)
            {
                GUILayout.Label(ControlLine(c), smallStyle);
                if (c.Targets.Count == 0)
                {
                    GUILayout.Label("      → no target", smallStyle);
                }
                foreach (TargetReading t in c.Targets)
                {
                    GUILayout.Label("      " + TargetLine(t), smallStyle);
                }
            }
            GUILayout.EndScrollView();

            GUILayout.Label("Journal:");
            journalScroll = GUILayout.BeginScrollView(journalScroll, GUILayout.Height(JOURNAL_HEIGHT));
            // Newest first: what just happened is at the top.
            for (int i = survey.Journal.Count - 1; i >= 0; i--)
            {
                GUILayout.Label(survey.Journal[i], smallStyle);
            }
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Log"))
            {
                WriteLog();
            }
            if (GUILayout.Button("Clear the journal"))
            {
                survey.ClearJournal();
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private static string WindowState(WindowReading w)
        {
            return w.Open ? (w.Pinned ? "open, pinned" : "open") : "closed";
        }

        private static string ControlLine(ControlReading c)
        {
            string buttons = c.Buttons.Count == 0 ? "no button" : string.Join(" ", c.Buttons.ToArray());
            return $"{c.Letter}  {c.Part}  {c.Resource}  {c.State}  [{buttons}]  created #{c.CreatedFrame}";
        }

        private static string TargetsLine(ControlReading c)
        {
            if (c.Targets.Count == 0)
            {
                return "no target";
            }
            StringBuilder text = new StringBuilder();
            foreach (TargetReading t in c.Targets)
            {
                if (text.Length > 0)
                {
                    text.Append(", ");
                }
                text.Append(TargetLine(t));
            }
            return text.ToString();
        }

        /// <summary>
        /// A target as the window writes it: its window and part, whether it is open, gone, and whether the
        /// pairing goes both ways (↔) or only from the control (→).
        /// </summary>
        private static string TargetLine(TargetReading t)
        {
            if (t.Destroyed)
            {
                return "→ † (destroyed)";
            }
            if (t.Letter == null)
            {
                return "→ ? (in no window)";
            }
            return $"{(t.BothWays ? "↔" : "→")} {t.Letter} {t.Part}{(t.WindowOpen ? "" : " (closed)")}";
        }

        /// <summary>
        /// Where a user interface element is on screen, in the GUI's coordinates: pixels from the top left
        /// corner of the game's window.
        /// </summary>
        private static Rect ScreenRect(RectTransform rect)
        {
            Canvas canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            // corners[0] is the bottom left corner, corners[2] the top right one; screen points count y from
            // the bottom.
            Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 topRight = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return new Rect(bottomLeft.x, Screen.height - topRight.y, topRight.x - bottomLeft.x, topRight.y - bottomLeft.y);
        }
    }
}
