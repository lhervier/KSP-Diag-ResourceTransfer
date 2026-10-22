using System;
using System.Collections.Generic;
using UnityEngine;
using com.github.lhervier.ksp.util.mcpserver;

namespace com.github.lhervier.ksp.diag.resourcetransfer
{
    /// <summary>
    /// What KSP-Util-MCPServer, when it is installed, offers of this mod as tools: what its window and badges
    /// show, its buttons, and the moving of the window. Each tool returns no more than the window shows or the
    /// log receives. Each method works on the mod loaded in the flight scene. Nothing here is needed to play by
    /// hand, and this mod runs the same without KSP-Util-MCPServer: only that server reads the attribute.
    /// </summary>
    internal static class McpTools
    {
        [McpTool("resourcetransfer_read",
            "Reads what KSP Diag - Resource Transfer shows, without writing anything: the frame, the part action " +
            "windows with their letter, part, and whether each is open and pinned; every resource transfer " +
            "control with its window's letter, its part, its resource, its state (None, In, Out), the buttons it " +
            "shows, the frame it was first seen on (createdFrame), what its badge writes (badge), and its " +
            "targets, each with the letter of its window (none when no window holds it), its part, whether that " +
            "window is open, whether the target is destroyed and whether the pairing goes both ways; and the " +
            "journal, oldest line first.")]
        internal static object Read()
        {
            KSPDiagResourceTransfer mod = Mod();
            Survey survey = mod.Survey;
            List<object> windows = new List<object>();
            foreach (WindowReading w in survey.Windows)
            {
                windows.Add(new Dictionary<string, object>
                {
                    { "letter", w.Letter },
                    { "part", w.Part },
                    { "open", w.Open },
                    { "pinned", w.Pinned }
                });
            }
            List<object> controls = new List<object>();
            foreach (ControlReading c in survey.Controls)
            {
                List<object> targets = new List<object>();
                foreach (TargetReading t in c.Targets)
                {
                    targets.Add(new Dictionary<string, object>
                    {
                        { "letter", t.Letter },
                        { "part", t.Part },
                        { "windowOpen", t.WindowOpen },
                        { "destroyed", t.Destroyed },
                        { "bothWays", t.BothWays }
                    });
                }
                controls.Add(new Dictionary<string, object>
                {
                    { "letter", c.Letter },
                    { "part", c.Part },
                    { "resource", c.Resource },
                    { "windowOpen", c.WindowOpen },
                    { "state", c.State },
                    { "buttons", new List<object>(c.Buttons.ToArray()) },
                    { "createdFrame", (double)c.CreatedFrame },
                    { "badge", "→ " + Survey.TargetLetters(c, int.MaxValue) },
                    { "targets", targets }
                });
            }
            return new Dictionary<string, object>
            {
                { "frame", (double)Time.frameCount },
                { "windows", windows },
                { "controls", controls },
                { "journal", new List<object>(survey.Journal) }
            };
        }

        [McpTool("resourcetransfer_log",
            "Presses the Log button of KSP Diag - Resource Transfer: writes to KSP.log what its window shows, then " +
            "the whole journal. Returns how many lines it wrote (lines).")]
        internal static object Log()
        {
            return new Dictionary<string, object> { { "lines", (double)Mod().WriteLog() } };
        }

        [McpTool("resourcetransfer_clear_journal",
            "Presses the Clear the journal button of KSP Diag - Resource Transfer. Returns how many lines the " +
            "journal holds now (lines).")]
        internal static object ClearJournal()
        {
            Survey survey = Mod().Survey;
            survey.ClearJournal();
            return new Dictionary<string, object> { { "lines", (double)survey.Journal.Count } };
        }

        [McpTool("resourcetransfer_move_window",
            "Moves the window of KSP Diag - Resource Transfer, as dragging it does: x and y in pixels from the top " +
            "left corner of the screen. Returns its position and size (x, y, width, height).")]
        internal static object MoveWindow(double x, double y)
        {
            KSPDiagResourceTransfer mod = Mod();
            Rect rect = mod.WindowRect;
            rect.x = (float)x;
            rect.y = (float)y;
            mod.WindowRect = rect;
            return new Dictionary<string, object>
            {
                { "x", (double)rect.x },
                { "y", (double)rect.y },
                { "width", (double)rect.width },
                { "height", (double)rect.height }
            };
        }

        [McpTool("resourcetransfer_show_window",
            "Shows or hides the window of KSP Diag - Resource Transfer, as Mod+F6 does; the badges and the journal " +
            "go on either way. Returns whether it shows (visible).")]
        internal static object ShowWindow(bool visible)
        {
            KSPDiagResourceTransfer.WindowVisible = visible;
            return new Dictionary<string, object> { { "visible", KSPDiagResourceTransfer.WindowVisible } };
        }

        private static KSPDiagResourceTransfer Mod()
        {
            KSPDiagResourceTransfer mod = UnityEngine.Object.FindObjectOfType<KSPDiagResourceTransfer>();
            if (mod == null)
            {
                throw new InvalidOperationException("KSP Diag - Resource Transfer only runs in flight");
            }
            return mod;
        }
    }
}
