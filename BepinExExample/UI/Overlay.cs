using UnityEngine;

namespace CrowdControl.UI;

/// <summary>
/// The mod's on-screen layer: a connection header and live rows for running timed effects.
///
/// Deliberately NOT an event feed. Instant effects announce themselves and are gone, so a line
/// saying one happened is stale by the time it is read - and a stream of them turns the panel into
/// scrolling debug output. What is worth screen space is what is still ACTIVE and how long is left
/// of it. System messages (connection, F8/F9) are the one exception and are shown briefly.
///
/// Drawn with IMGUI from the mod's OnGUI rather than built out of the game's own UI. Most games have
/// no general-purpose "show some text with a countdown" entry point, and borrowing one that exists
/// (a chat box, a subtitle line) puts mod output where players expect something else. IMGUI is
/// self-contained, cannot disturb the game's own HUD, and disappears entirely when switched off.
///
/// Anchored to the TOP RIGHT of the window. Move it by changing the <see cref="Draw"/> card rect -
/// pick whichever corner your game's own HUD leaves clear.
///
/// If your game pauses by setting <c>Time.timeScale = 0</c>, make sure the mod ticks from Update
/// rather than FixedUpdate: Unity stops calling FixedUpdate at timeScale 0, so effects would freeze
/// mid-countdown without ever registering as paused, here or in the Crowd Control app.
///
/// Styled after the Crowd Control app - see <see cref="CcTheme"/> - so it reads as part of the
/// same product rather than as debug output: a dark purple card with a rounded border, a brand
/// accent stripe, and effect rows with draining progress bars.
/// </summary>
public static class Overlay
{
    private const float MARGIN = 14f;
    private const float WIDTH = 240f;
    private const float PAD = 7f;
    private const float ACCENT = 2f;   //brand stripe down the left edge
    private const float RADIUS = 5f;
    private const float HEADER = 16f;
    private const float ROW = 15f;
    private const float BAR = 3f;
    private const float GAP = 3f;

    private const int MAX_TEXT = 34;

    /// <summary>
    /// Effect announcements and system messages share this list. Everything is Crab fires a lot of
    /// small stat effects, so the cap is what keeps a busy stream from turning the card into a wall.
    /// </summary>
    private const int MAX_LINES = 4;

    private readonly struct Line
    {
        public readonly string Text;
        public readonly float Expires;
        public readonly float Born;
        public Line(string text, float born, float expires) { Text = text; Born = born; Expires = expires; }
    }

    private static readonly List<Line> Lines = new();

    /// <summary>
    /// Runtime visibility, toggled with F8. Separate from the settings opt-out: the setting is the
    /// streamer's standing preference, this is "hide it for a moment".
    /// </summary>
    private static bool _hidden;

    private static Texture2D _pixel;
    private static Texture2D _panel;      //rounded card, regenerated when the size changes
    private static int _panelW, _panelH;
    private static GUIStyle _name, _meta, _metaRight, _msg;

    /// <summary>Flips the overlay on or off. Returns the new state.</summary>
    public static bool Toggle()
    {
        _hidden = !_hidden;
        return !_hidden;
    }

    /// <summary>
    /// Queues a short system message - connection state, hotkey feedback.
    /// </summary>
    /// <param name="force">
    /// True to show it even when the overlay is hidden or messages are switched off. Used for
    /// connection feedback, which is the one thing a player needs to see precisely when they have
    /// turned the UI off and are wondering why nothing is happening.
    /// </param>
    public static void Show(string text, bool force = false)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (!force && (!ModSettings.ShowMessages || _hidden)) return;

        if (force) _hidden = false;

        float now = Now();
        lock (Lines)
        {
            Lines.Add(new Line(Trim(text), now, now + ModSettings.MessageSeconds));
            while (Lines.Count > MAX_LINES) Lines.RemoveAt(0);
        }
    }

    /// <summary>Clears everything on screen.</summary>
    public static void Clear()
    {
        lock (Lines) Lines.Clear();
    }

    /// <summary>Draws the overlay. Call from OnGUI only.</summary>
    /// <param name="connected">True if the mod has a live connection to the Crowd Control app.</param>
    /// <param name="clientPresent">
    /// True if the Crowd Control app appears to be running at all.
    /// </param>
    /// <remarks>
    /// The two are deliberately separate. Someone who installed the mod but is just playing the
    /// game normally should see NOTHING - a permanent red "disconnected" badge would be noise about
    /// a program they never started. The badge only means something once Crowd Control is actually
    /// running, at which point "not connected" is a real problem worth surfacing.
    /// </remarks>
    public static void Draw(bool connected, bool clientPresent)
    {
        try
        {
            if (_hidden) return;

            EnsureStyles();

            ActiveEffect[] active = Scheduler.ActiveTimedEffects();
            Line[] lines = TakeLines();

            //no Crowd Control running and nothing of ours happening - stay out of the way entirely
            bool ccRunning = clientPresent || connected;
            bool header = ModSettings.ShowIndicator && ccRunning;

            if (!header && (active.Length == 0) && (lines.Length == 0)) return;

            //measure so the card is exactly as tall as its contents
            float h = PAD;
            if (header) h += HEADER + GAP;
            if (active.Length > 0) h += (active.Length * (ROW + BAR + GAP)) - GAP;
            if (lines.Length > 0)
            {
                if (active.Length > 0) h += GAP + 1f + GAP; //divider between sections
                h += lines.Length * ROW;
            }

            h += PAD;

            //anchored to the top right, so the card grows downwards and leftwards away from the edge
            Rect card = new(Screen.width - WIDTH - MARGIN, MARGIN, WIDTH, h);
            DrawCard(card);

            float x = card.x + ACCENT + PAD;
            float w = WIDTH - ACCENT - (PAD * 2f);
            float y = card.y + PAD;

            if (header)
            {
                DrawHeader(x, y, w, connected);
                y += HEADER + GAP;
            }

            for (int i = 0; i < active.Length; i++)
            {
                DrawEffect(x, y, w, active[i]);
                y += ROW + BAR + GAP;
            }

            if (lines.Length > 0)
            {
                if (active.Length > 0)
                {
                    Fill(new Rect(x, y, w, 1f), CcTheme.Slate500);
                    y += 1f + GAP;
                }

                float now = Now();
                foreach (Line line in lines)
                {
                    //ease in quickly, fade out over the last second, so lines arrive and leave
                    //gently instead of popping
                    float appear = Mathf.Clamp01((now - line.Born) / 0.15f);
                    float remain = Mathf.Clamp01(line.Expires - now);
                    float alpha = Mathf.Min(appear, remain);

                    DrawText(_msg, new Rect(x, y, w, ROW), line.Text, CcTheme.White200.Fade(alpha));
                    y += ROW;
                }
            }
        }
        catch {/* a broken overlay must never take the game down */}
    }

    private static void DrawCard(Rect card)
    {
        //border first, then the fill inset by a pixel - cheaper than a nine-slice and crisp at any size
        Texture2D rounded = GetPanel((int)card.width, (int)card.height);

        Color previous = GUI.color;

        GUI.color = CcTheme.Slate500;
        GUI.DrawTexture(card, rounded);

        GUI.color = CcTheme.Slate800.Fade(0.96f);
        GUI.DrawTexture(new Rect(card.x + 1f, card.y + 1f, card.width - 2f, card.height - 2f), rounded);

        //brand stripe down the left edge, the app's card motif
        GUI.color = CcTheme.Royal50;
        GUI.DrawTexture(new Rect(card.x + 1f, card.y + 3f, ACCENT, card.height - 6f), _pixel);

        GUI.color = previous;
    }

    private static void DrawHeader(float x, float y, float w, bool connected)
    {
        const float dot = 8f;

        Color status = connected ? CcTheme.Teal200 : CcTheme.Red200;

        //soft halo behind the dot so the status reads at a glance
        Fill(new Rect(x - 2f, y + ((HEADER - dot) / 2f) - 2f, dot + 4f, dot + 4f), status.Fade(0.18f));
        Fill(new Rect(x, y + ((HEADER - dot) / 2f), dot, dot), status);

        DrawText(_meta, new Rect(x + dot + 6f, y, w - dot - 6f, HEADER),
            "CROWD CONTROL", connected ? CcTheme.White300 : CcTheme.Red200);

        if (!connected)
            DrawText(_metaRight, new Rect(x, y, w, HEADER), "F9", CcTheme.Red200);
    }

    private static void DrawEffect(float x, float y, float w, ActiveEffect e)
    {
        Color accent = e.Paused ? CcTheme.Yellow300 : CcTheme.Royal50;

        DrawText(_name, new Rect(x, y, w - 44f, ROW), e.Label,
            e.Paused ? CcTheme.White300 : CcTheme.White100);

        //the countdown is white for legibility - the brand purple is right for the bar, but as
        //small text over a dark panel it was barely readable. "paused" keeps the amber, because
        //there it is the state being communicated rather than a number to read.
        DrawText(_metaRight, new Rect(x, y, w, ROW),
            e.Paused ? "paused" : FormatTime(e.Remaining),
            e.Paused ? CcTheme.Yellow300 : CcTheme.White100);

        float barY = y + ROW;
        Fill(new Rect(x, barY, w, BAR), CcTheme.Slate600);

        float fraction = (e.Duration > 0f) ? Mathf.Clamp01(e.Remaining / e.Duration) : 0f;
        if (fraction > 0f)
        {
            Rect fill = new(x, barY, w * fraction, BAR);
            Fill(fill, accent.Fade(e.Paused ? 0.5f : 1f));

            //a lighter leading edge gives the bar some depth rather than a flat block
            if (!e.Paused && (fill.width > 2f))
                Fill(new Rect(fill.xMax - 2f, barY, 2f, BAR), CcTheme.Blurple200);
        }
    }

    private static Line[] TakeLines()
    {
        if (!ModSettings.ShowMessages) return Array.Empty<Line>();

        float now = Now();
        lock (Lines)
        {
            //expire here rather than on a timer - drawing is the only thing that cares
            for (int i = Lines.Count - 1; i >= 0; i--)
                if (Lines[i].Expires <= now) Lines.RemoveAt(i);

            return Lines.ToArray();
        }
    }

    private static string FormatTime(float seconds)
    {
        if (seconds < 0f) seconds = 0f;
        int total = Mathf.CeilToInt(seconds);
        return (total >= 60) ? $"{total / 60}:{total % 60:00}" : $"{total}s";
    }

    private static string Trim(string text) =>
        (text.Length <= MAX_TEXT) ? text : text.Substring(0, MAX_TEXT - 1).TrimEnd() + "…";

    private static void DrawText(GUIStyle style, Rect rect, string text, Color color)
    {
        if (string.IsNullOrEmpty(text)) return;

        Color previous = GUI.color;
        GUI.color = color;
        GUI.Label(rect, text, style);
        GUI.color = previous;
    }

    private static void Fill(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, _pixel);
        GUI.color = previous;
    }

    /// <summary>
    /// A white rounded rectangle to tint per draw.
    /// </summary>
    /// <remarks>
    /// IMGUI has no rounded primitive, and the card changes height as effects come and go, so the
    /// texture is rebuilt only when the size actually changes rather than every frame.
    /// </remarks>
    private static Texture2D GetPanel(int w, int h)
    {
        if ((_panel != null) && (_panelW == w) && (_panelH == h)) return _panel;

        if (_panel != null) UnityEngine.Object.Destroy(_panel);

        _panel = new Texture2D(w, h, TextureFormat.ARGB32, false) { hideFlags = HideFlags.HideAndDontSave };
        _panelW = w;
        _panelH = h;

        Color[] pixels = new Color[w * h];
        float r = RADIUS;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                //distance from the nearest corner centre, so only the corners get carved away
                float dx = (x < r) ? (r - x) : ((x >= w - r) ? (x - (w - r - 1)) : 0f);
                float dy = (y < r) ? (r - y) : ((y >= h - r) ? (y - (h - r - 1)) : 0f);

                float alpha = 1f;
                if ((dx > 0f) && (dy > 0f))
                {
                    //antialias the arc over one pixel so the corners are not jagged
                    float d = Mathf.Sqrt((dx * dx) + (dy * dy));
                    alpha = Mathf.Clamp01(r - d + 0.5f);
                }

                pixels[(y * w) + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        _panel.SetPixels(pixels);
        _panel.Apply();
        return _panel;
    }

    private static void EnsureStyles()
    {
        if (_pixel == null)
        {
            _pixel = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            _pixel.SetPixel(0, 0, Color.white);
            _pixel.Apply();
        }

        _name ??= new GUIStyle(GUI.skin.label)
        { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(0, 0, 0, 0), wordWrap = false };

        _meta ??= new GUIStyle(GUI.skin.label)
        { fontSize = 10, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(0, 0, 0, 0), wordWrap = false };

        _metaRight ??= new GUIStyle(GUI.skin.label)
        { fontSize = 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight, padding = new RectOffset(0, 0, 0, 0), wordWrap = false };

        _msg ??= new GUIStyle(GUI.skin.label)
        { fontSize = 11, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(0, 0, 0, 0), wordWrap = false };
    }

    private static float Now()
    {
        try { return Time.realtimeSinceStartup; }
        catch { return 0f; }
    }

    /// <summary>A running timed effect, as the overlay needs to see it.</summary>
    public readonly struct ActiveEffect
    {
        public readonly string Label;
        public readonly float Remaining;
        public readonly float Duration;
        public readonly bool Paused;

        public ActiveEffect(string label, float remaining, float duration, bool paused)
        {
            Label = label;
            Remaining = remaining;
            Duration = duration;
            Paused = paused;
        }
    }
}
