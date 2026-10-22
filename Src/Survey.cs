using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace com.github.lhervier.ksp.diag.resourcetransfer
{
    /// <summary>One part action window, as the survey reads it.</summary>
    internal sealed class WindowReading
    {
        public UIPartActionWindow Window;
        public string Letter;
        public string Part;
        public bool Open;
        public bool Pinned;
    }

    /// <summary>One target of a transfer control: another control it is paired with.</summary>
    internal sealed class TargetReading
    {
        /// <summary>The letter of the target's window; null when no window holds it any more.</summary>
        public string Letter;
        public string Part;
        /// <summary>Whether the target's window is open.</summary>
        public bool WindowOpen;
        /// <summary>Whether the target no longer exists.</summary>
        public bool Destroyed;
        /// <summary>Whether the target has the control among its own targets.</summary>
        public bool BothWays;
    }

    /// <summary>One resource transfer control, as the survey reads it.</summary>
    internal sealed class ControlReading
    {
        public UIPartActionResourceTransfer Control;
        public string Letter;
        public string Part;
        public string Resource;
        public bool WindowOpen;
        public string State;
        public List<string> Buttons = new List<string>();
        public List<TargetReading> Targets = new List<TargetReading>();
        /// <summary>The frame the control was first seen on.</summary>
        public int CreatedFrame;
    }

    /// <summary>
    /// Reads the part action windows of the flight scene and the resource transfer controls they hold, once per
    /// frame, and keeps a journal of what changes from one frame to the next. Reads only: nothing in the game is
    /// changed.
    /// </summary>
    internal sealed class Survey
    {
        /// <summary>How many lines the journal keeps.</summary>
        private const int JOURNAL_SIZE = 500;

        private readonly Dictionary<int, string> letters = new Dictionary<int, string>();
        private int lettersGiven;
        private readonly Dictionary<int, int> createdFrames = new Dictionary<int, int>();

        // What the last frame read, to tell what changed: by the instance id of each control and window.
        private Dictionary<int, Snapshot> lastControls = new Dictionary<int, Snapshot>();
        private Dictionary<int, bool> lastWindows = new Dictionary<int, bool>();

        private readonly List<string> journal = new List<string>();

        /// <summary>The windows read on the last frame: open ones first, in the order KSP lists them.</summary>
        public List<WindowReading> Windows { get; private set; } = new List<WindowReading>();

        /// <summary>The transfer controls read on the last frame, window by window.</summary>
        public List<ControlReading> Controls { get; private set; } = new List<ControlReading>();

        /// <summary>The journal, oldest line first.</summary>
        public IList<string> Journal => journal;

        /// <summary>Empties the journal.</summary>
        public void ClearJournal()
        {
            journal.Clear();
        }

        /// <summary>Reads the windows and their transfer controls as they are on frame <paramref name="frame"/>.</summary>
        public void Read(int frame)
        {
            List<WindowReading> windows = new List<WindowReading>();
            UIPartActionController controller = UIPartActionController.Instance;
            if (controller != null)
            {
                AddWindows(windows, controller.windows, true);
                AddWindows(windows, controller.hiddenWindows, false);
                AddWindows(windows, controller.hiddenResourceWindows, false);
            }

            // Which window holds each control, to name the targets.
            Dictionary<int, WindowReading> holders = new Dictionary<int, WindowReading>();
            List<KeyValuePair<UIPartActionResourceTransfer, WindowReading>> found =
                new List<KeyValuePair<UIPartActionResourceTransfer, WindowReading>>();
            foreach (WindowReading w in windows)
            {
                foreach (UIPartActionItem item in w.Window.ListItems)
                {
                    UIPartActionResourceTransfer control = item as UIPartActionResourceTransfer;
                    if (control != null)
                    {
                        holders[control.GetInstanceID()] = w;
                        found.Add(new KeyValuePair<UIPartActionResourceTransfer, WindowReading>(control, w));
                    }
                }
            }

            List<ControlReading> controls = new List<ControlReading>();
            foreach (KeyValuePair<UIPartActionResourceTransfer, WindowReading> pair in found)
            {
                controls.Add(ReadControl(pair.Key, pair.Value, holders, frame));
            }

            NoteChanges(frame, windows, controls);
            Windows = windows;
            Controls = controls;
        }

        /// <summary>
        /// Returns the targets of a control as its badge writes them: the letters of their windows, in brackets
        /// when the window is closed, a dagger for a target that no longer exists, a dash for none.
        /// </summary>
        public static string TargetLetters(ControlReading c, int max)
        {
            if (c.Targets.Count == 0)
            {
                return "—";
            }
            StringBuilder text = new StringBuilder();
            int shown = 0;
            foreach (TargetReading t in c.Targets)
            {
                if (shown == max)
                {
                    text.Append(" +").Append(c.Targets.Count - max);
                    break;
                }
                if (shown > 0)
                {
                    text.Append(",");
                }
                text.Append(TargetLetter(t));
                shown++;
            }
            return text.ToString();
        }

        /// <summary>A target as a badge writes it: its window's letter, in brackets when closed, † when gone.</summary>
        public static string TargetLetter(TargetReading t)
        {
            if (t.Destroyed || t.Letter == null)
            {
                return "†";
            }
            return t.WindowOpen ? t.Letter : "(" + t.Letter + ")";
        }

        private void AddWindows(List<WindowReading> into, List<UIPartActionWindow> from, bool listedOpen)
        {
            if (from == null)
            {
                return;
            }
            foreach (UIPartActionWindow w in from)
            {
                if (w == null || w.part == null)
                {
                    continue;
                }
                into.Add(new WindowReading
                {
                    Window = w,
                    Letter = LetterOf(w),
                    Part = w.part.partInfo.title,
                    Open = listedOpen && w.gameObject.activeInHierarchy,
                    Pinned = w.Pinned
                });
            }
        }

        /// <summary>The letter of a window: given on the first frame it is seen, kept for as long as it exists.</summary>
        private string LetterOf(UIPartActionWindow w)
        {
            int id = w.GetInstanceID();
            string letter;
            if (!letters.TryGetValue(id, out letter))
            {
                letter = Letter(lettersGiven++);
                letters[id] = letter;
            }
            return letter;
        }

        /// <summary>A, B, ..., Z, then AA, AB...</summary>
        private static string Letter(int index)
        {
            string letter = "";
            index++;
            while (index > 0)
            {
                index--;
                letter = (char)('A' + index % 26) + letter;
                index /= 26;
            }
            return letter;
        }

        private ControlReading ReadControl(UIPartActionResourceTransfer control, WindowReading window,
            Dictionary<int, WindowReading> holders, int frame)
        {
            int id = control.GetInstanceID();
            int created;
            if (!createdFrames.TryGetValue(id, out created))
            {
                created = frame;
                createdFrames[id] = frame;
            }
            ControlReading reading = new ControlReading
            {
                Control = control,
                Letter = window.Letter,
                Part = window.Part,
                Resource = control.Resource != null ? control.Resource.resourceName : "?",
                WindowOpen = window.Open,
                State = control.State.ToString(),
                CreatedFrame = created
            };
            if (control.flowInBtn != null && control.flowInBtn.gameObject.activeSelf)
            {
                reading.Buttons.Add("In");
            }
            if (control.flowOutBtn != null && control.flowOutBtn.gameObject.activeSelf)
            {
                reading.Buttons.Add("Out");
            }
            if (control.flowStopBtn != null && control.flowStopBtn.gameObject.activeSelf)
            {
                reading.Buttons.Add("Stop");
            }
            if (control.Targets == null)
            {
                return reading;
            }
            foreach (UIPartActionResourceTransfer target in control.Targets)
            {
                TargetReading t = new TargetReading();
                // A destroyed control compares equal to null, but its instance id is still there.
                if ((object)target == null || target == null)
                {
                    t.Destroyed = true;
                }
                WindowReading holder;
                if ((object)target != null && holders.TryGetValue(target.GetInstanceID(), out holder))
                {
                    t.Letter = holder.Letter;
                    t.Part = holder.Part;
                    t.WindowOpen = holder.Open;
                }
                if (!t.Destroyed && target.Targets != null)
                {
                    t.BothWays = target.Targets.Contains(control);
                }
                reading.Targets.Add(t);
            }
            return reading;
        }

        /// <summary>Writes to the journal what changed since the last frame read.</summary>
        private void NoteChanges(int frame, List<WindowReading> windows, List<ControlReading> controls)
        {
            Dictionary<int, bool> nowWindows = new Dictionary<int, bool>();
            foreach (WindowReading w in windows)
            {
                int id = w.Window.GetInstanceID();
                nowWindows[id] = w.Open;
                bool wasOpen;
                bool known = lastWindows.TryGetValue(id, out wasOpen);
                if (w.Open && (!known || !wasOpen))
                {
                    Note(frame, $"window {w.Letter} ({w.Part}) opened");
                }
                else if (!w.Open && known && wasOpen)
                {
                    Note(frame, $"window {w.Letter} ({w.Part}) closed");
                }
            }
            foreach (KeyValuePair<int, bool> pair in lastWindows)
            {
                if (!nowWindows.ContainsKey(pair.Key))
                {
                    string letter;
                    letters.TryGetValue(pair.Key, out letter);
                    Note(frame, $"window {letter} destroyed");
                }
            }
            lastWindows = nowWindows;

            Dictionary<int, Snapshot> nowControls = new Dictionary<int, Snapshot>();
            foreach (ControlReading c in controls)
            {
                int id = c.Control.GetInstanceID();
                Snapshot now = new Snapshot(c);
                nowControls[id] = now;
                Snapshot before;
                if (!lastControls.TryGetValue(id, out before))
                {
                    Note(frame, $"{now.Name}: created, → {now.Targets}");
                    continue;
                }
                if (before.State != now.State)
                {
                    Note(frame, $"{now.Name}: {before.State} → {now.State}");
                }
                if (before.Targets != now.Targets)
                {
                    Note(frame, $"{now.Name}: targets {before.Targets} → {now.Targets}");
                }
            }
            foreach (KeyValuePair<int, Snapshot> pair in lastControls)
            {
                if (!nowControls.ContainsKey(pair.Key))
                {
                    Note(frame, $"{pair.Value.Name}: destroyed");
                    createdFrames.Remove(pair.Key);
                }
            }
            lastControls = nowControls;
        }

        private void Note(int frame, string line)
        {
            journal.Add($"#{frame} {line}");
            if (journal.Count > JOURNAL_SIZE)
            {
                journal.RemoveAt(0);
            }
        }

        /// <summary>What the journal compares of a control from one frame to the next.</summary>
        private sealed class Snapshot
        {
            public readonly string Name;
            public readonly string State;
            public readonly string Targets;

            public Snapshot(ControlReading c)
            {
                Name = $"{c.Letter} {c.Resource}";
                State = c.State;
                Targets = TargetLetters(c, int.MaxValue);
            }
        }
    }
}
