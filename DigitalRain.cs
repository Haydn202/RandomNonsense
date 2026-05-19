using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

class Program
{
    const string Chars = "ｱｲｳｴｵｶｷｸｹｺｻｼｽｾｿﾀﾁﾂﾃﾄﾅﾆﾇﾈﾉﾊﾋﾌﾍﾎﾏﾐﾑﾒﾓﾔﾕﾖﾗﾘﾙﾚﾛﾜﾝ0123456789ABCDEF";

    const int TargetFrameMs = 16;
    const int ResizeCheckInterval = 30;

    const short AttrEmpty = 0;
    const short AttrHead = 0x000A;
    const short AttrTrail = 0x0002;
    const short AttrFade = 0x0008;

    static readonly IntPtr ConsoleOut = GetStdHandle(-11);
    static readonly string[] AnsiColor = { "", "\x1b[92m", "\x1b[32m", "\x1b[90m" };

    static bool _useWin32;
    static bool _useAnsiAltScreen;
    static char[] _chars = Array.Empty<char>();
    static short[] _attrs = Array.Empty<short>();
    static CharInfo[] _consoleBuffer = Array.Empty<CharInfo>();
    static readonly StringBuilder _frame = new(8192);

    static void Main()
    {
        _useWin32 = TryInitWin32Console();
        if (!_useWin32)
            InitAnsiTerminal();

        var rng = new Random();

        int width = 80, height = 40;
        RefreshDimensions(ref width, ref height);
        EnsureBuffers(width, height);

        var drops = new Drop[width];
        for (int c = 0; c < width; c++)
            drops[c] = Drop.Create(rng, height);

        ClearScreen(width, height);
        var frameTimer = System.Diagnostics.Stopwatch.StartNew();
        int frame = 0;

        try
        {
            while (true)
            {
                if (frame % ResizeCheckInterval == 0)
                {
                    int prevWidth = width;
                    RefreshDimensions(ref width, ref height);
                    if (width != prevWidth)
                    {
                        var next = new Drop[width];
                        for (int c = 0; c < width; c++)
                            next[c] = c < drops.Length ? drops[c] : Drop.Create(rng, height);
                        drops = next;
                    }
                    EnsureBuffers(width, height);
                }

                Array.Fill(_chars, ' ');
                Array.Fill(_attrs, AttrEmpty);

                for (int c = 0; c < width; c++)
                {
                    var drop = drops[c];
                    drop.Update(rng, height);
                    drop.Draw(_chars, _attrs, c, height, width);
                    drops[c] = drop;
                }

                if (_useWin32)
                    BlitWin32(width, height);
                else
                    BlitAnsi(width, height);

                frame++;
                int elapsed = (int)frameTimer.ElapsedMilliseconds;
                frameTimer.Restart();
                int sleep = TargetFrameMs - elapsed;
                if (sleep > 0)
                    Thread.Sleep(sleep);
            }
        }
        finally
        {
            if (_useWin32)
                SetWin32CursorVisible(true);
            else if (_useAnsiAltScreen)
                Console.Write("\x1b[?1049l\x1b[?25h\x1b[0m");
        }
    }

    static bool TryInitWin32Console()
    {
        if (!OperatingSystem.IsWindows())
            return false;
        if (Environment.GetEnvironmentVariable("MSYSTEM") is { Length: > 0 })
            return false;
        if (Console.IsOutputRedirected)
            return false;
        if (ConsoleOut == IntPtr.Zero || ConsoleOut == new IntPtr(-1))
            return false;
        if (GetFileType(ConsoleOut) != 2)
            return false;
        if (!GetConsoleScreenBufferInfo(ConsoleOut, out _))
            return false;

        SetWin32CursorVisible(false);
        return true;
    }

    static void InitAnsiTerminal()
    {
        EnableVirtualTerminal();
        Console.OutputEncoding = Encoding.UTF8;
        Console.Write("\x1b[?1049h\x1b[?25l\x1b[2J\x1b[H");
        _useAnsiAltScreen = true;
        try { Console.CursorVisible = false; } catch { }
    }

    static void BlitWin32(int width, int height)
    {
        int size = width * height;
        for (int i = 0; i < size; i++)
        {
            _consoleBuffer[i].Character = _chars[i];
            _consoleBuffer[i].Attributes = _attrs[i];
        }

        var bufferSize = new Coord { X = (short)width, Y = (short)height };
        var bufferOrigin = new Coord { X = 0, Y = 0 };
        var writeRegion = new SmallRect
        {
            Left = 0,
            Top = 0,
            Right = (short)(width - 1),
            Bottom = (short)(height - 1)
        };

        WriteConsoleOutput(ConsoleOut, _consoleBuffer, bufferSize, bufferOrigin, ref writeRegion);
    }

    static void BlitAnsi(int width, int height)
    {
        _frame.Clear();
        _frame.Append("\x1b[H");

        for (int y = 0; y < height; y++)
        {
            if (y > 0)
            {
                _frame.Append("\x1b[");
                _frame.Append(y + 1);
                _frame.Append(";1H");
            }

            int row = y * width;
            short currentAttr = -1;
            for (int x = 0; x < width; x++)
            {
                short attr = _attrs[row + x];
                if (attr != currentAttr)
                {
                    if (currentAttr != AttrEmpty)
                        _frame.Append("\x1b[0m");
                    if (attr != AttrEmpty)
                        _frame.Append(AttrToAnsi(attr));
                    currentAttr = attr;
                }
                _frame.Append(_chars[row + x]);
            }
            if (currentAttr != AttrEmpty)
                _frame.Append("\x1b[0m");
            _frame.Append("\x1b[K");
        }

        Console.Out.Write(_frame.ToString());
    }

    static string AttrToAnsi(short attr) => attr switch
    {
        AttrHead => AnsiColor[1],
        AttrTrail => AnsiColor[2],
        AttrFade => AnsiColor[3],
        _ => ""
    };

    static void ClearScreen(int width, int height)
    {
        Array.Fill(_chars, ' ');
        Array.Fill(_attrs, AttrEmpty);
        if (_useWin32)
            BlitWin32(width, height);
        else
            Console.Write("\x1b[2J\x1b[H");
    }

    static void EnsureBuffers(int width, int height)
    {
        int size = width * height;
        if (_chars.Length == size)
            return;

        _chars = new char[size];
        _attrs = new short[size];
        _consoleBuffer = new CharInfo[size];
    }

    static void RefreshDimensions(ref int width, ref int height)
    {
        if (_useWin32 && GetConsoleScreenBufferInfo(ConsoleOut, out var info))
        {
            width = info.srWindow.Right - info.srWindow.Left + 1;
            height = info.srWindow.Bottom - info.srWindow.Top + 1;
            width = Math.Max(20, width);
            height = Math.Max(10, height);
            return;
        }

        try
        {
            width = Math.Max(20, Console.WindowWidth);
            height = Math.Max(10, Console.WindowHeight);
        }
        catch
        {
            width = 80;
            height = 40;
        }
    }

    static void SetWin32CursorVisible(bool visible)
    {
        var info = new ConsoleCursorInfo { Size = 1, Visible = visible };
        SetConsoleCursorInfo(ConsoleOut, ref info);
        try { Console.CursorVisible = visible; } catch { }
    }

    static void EnableVirtualTerminal()
    {
        if (!OperatingSystem.IsWindows())
            return;
        const uint enableVirtualTerminalProcessing = 0x0004;
        if (!GetConsoleMode(ConsoleOut, out uint mode))
            return;
        SetConsoleMode(ConsoleOut, mode | enableVirtualTerminalProcessing);
    }

    [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
    struct CharInfo
    {
        [FieldOffset(0)] public char Character;
        [FieldOffset(2)] public short Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct Coord
    {
        public short X;
        public short Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct SmallRect
    {
        public short Left;
        public short Top;
        public short Right;
        public short Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct ConsoleScreenBufferInfo
    {
        public Coord dwSize;
        public Coord dwCursorPosition;
        public short wAttributes;
        public SmallRect srWindow;
        public Coord dwMaximumWindowSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct ConsoleCursorInfo
    {
        public int Size;
        public bool Visible;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern uint GetFileType(IntPtr hFile);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool WriteConsoleOutput(
        IntPtr hConsoleOutput,
        [MarshalAs(UnmanagedType.LPArray)] CharInfo[] lpBuffer,
        Coord dwBufferSize,
        Coord dwBufferCoord,
        ref SmallRect lpWriteRegion);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GetConsoleScreenBufferInfo(
        IntPtr hConsoleOutput,
        out ConsoleScreenBufferInfo lpConsoleScreenBufferInfo);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool SetConsoleCursorInfo(IntPtr hConsoleOutput, ref ConsoleCursorInfo lpConsoleCursorInfo);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

    struct Drop
    {
        public double HeadY;
        public int Length;
        public double Speed;
        public char[] Trail;

        public static Drop Create(Random rng, int height)
        {
            int length = rng.Next(4, Math.Min(20, height / 2 + 1));
            var trail = new char[length];
            for (int i = 0; i < length; i++)
                trail[i] = RandomChar(rng);
            return new Drop
            {
                HeadY = rng.NextDouble() * -length,
                Length = length,
                Speed = 0.22 + rng.NextDouble() * 0.18,
                Trail = trail
            };
        }

        public void Update(Random rng, int height)
        {
            if (rng.Next(8) == 0)
                Trail[rng.Next(Trail.Length)] = RandomChar(rng);

            HeadY += Speed;
            if (HeadY - Length > height)
            {
                var fresh = Create(rng, height);
                HeadY = fresh.HeadY;
                Length = fresh.Length;
                Speed = fresh.Speed;
                Trail = fresh.Trail;
            }
        }

        public void Draw(char[] chars, short[] attrs, int column, int height, int width)
        {
            if (column < 0 || column >= width)
                return;

            for (int i = 0; i < Length; i++)
            {
                int y = (int)(HeadY - i);
                if (y < 0 || y >= height)
                    continue;

                int idx = y * width + column;
                chars[idx] = Trail[i];
                attrs[idx] = i == 0 ? AttrHead : i < Length / 3 ? AttrTrail : AttrFade;
            }
        }

        static char RandomChar(Random rng) => Chars[rng.Next(Chars.Length)];
    }
}
