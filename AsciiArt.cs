using System;
using System.Threading;

class Program
{
    static void Main()
    {
        const int size = 10;
        const double rotationSpeed = 0.1;
        double angleX = 0;
        double angleY = 0;

        Console.CursorVisible = false;
        
        while (true)
        {
            // Move cursor to top-left instead of clearing to avoid scrolling
            try
            {
                Console.SetCursorPosition(0, 0);
            }
            catch
            {
                // If buffer is too small, use Clear insteadcd
                Console.Clear();
            }
            
            // Define cube vertices (centered at origin)
            double[,] vertices = new double[,]
            {
                {-1, -1, -1}, {1, -1, -1}, {1, 1, -1}, {-1, 1, -1},
                {-1, -1, 1}, {1, -1, 1}, {1, 1, 1}, {-1, 1, 1}
            };

            // Rotate vertices
            double[,] rotated = new double[8, 3];
            for (int i = 0; i < 8; i++)
            {
                double x = vertices[i, 0];
                double y = vertices[i, 1];
                double z = vertices[i, 2];

                // Rotate around Y axis
                double tempX = x * Math.Cos(angleY) - z * Math.Sin(angleY);
                double tempZ = x * Math.Sin(angleY) + z * Math.Cos(angleY);
                x = tempX;
                z = tempZ;

                // Rotate around X axis
                double tempY = y * Math.Cos(angleX) - z * Math.Sin(angleX);
                z = y * Math.Sin(angleX) + z * Math.Cos(angleX);
                y = tempY;

                rotated[i, 0] = x;
                rotated[i, 1] = y;
                rotated[i, 2] = z;
            }

            // Project to 2D and draw
            char[,] screen = new char[40, 40];
            for (int i = 0; i < 40; i++)
                for (int j = 0; j < 40; j++)
                    screen[i, j] = ' ';

            // Draw edges
            int[][] edges = new int[][]
            {
                new int[] {0, 1}, new int[] {1, 2}, new int[] {2, 3}, new int[] {3, 0},
                new int[] {4, 5}, new int[] {5, 6}, new int[] {6, 7}, new int[] {7, 4},
                new int[] {0, 4}, new int[] {1, 5}, new int[] {2, 6}, new int[] {3, 7}
            };

            foreach (var edge in edges)
            {
                int x1 = (int)(rotated[edge[0], 0] * size) + 20;
                int y1 = (int)(rotated[edge[0], 1] * size) + 20;
                int x2 = (int)(rotated[edge[1], 0] * size) + 20;
                int y2 = (int)(rotated[edge[1], 1] * size) + 20;

                DrawLine(screen, x1, y1, x2, y2);
            }

            // Draw vertices
            for (int i = 0; i < 8; i++)
            {
                int x = (int)(rotated[i, 0] * size) + 20;
                int y = (int)(rotated[i, 1] * size) + 20;
                if (x >= 0 && x < 40 && y >= 0 && y < 40)
                    screen[y, x] = '●';
            }

            // Print screen - only write lines that fit in the buffer
            int maxRows = 40;
            try
            {
                maxRows = Math.Min(40, Console.BufferHeight);
            }
            catch
            {
                maxRows = Math.Min(40, Console.WindowHeight);
            }
            
            for (int i = 0; i < maxRows; i++)
            {
                try
                {
                    Console.SetCursorPosition(0, i);
                    for (int j = 0; j < 40; j++)
                        Console.Write(screen[i, j]);
                    // Clear rest of line if window is wider
                    try
                    {
                        if (Console.WindowWidth > 40)
                            Console.Write(new string(' ', Console.WindowWidth - 40));
                    }
                    catch { }
                }
                catch
                {
                    // If we can't set cursor position, just write the line
                    for (int j = 0; j < 40; j++)
                        Console.Write(screen[i, j]);
                    Console.WriteLine();
                }
            }

            angleX += rotationSpeed;
            angleY += rotationSpeed * 0.7;
            Thread.Sleep(1);
        }
    }

    static void DrawLine(char[,] screen, int x1, int y1, int x2, int y2)
    {
        int dx = Math.Abs(x2 - x1);
        int dy = Math.Abs(y2 - y1);
        int sx = x1 < x2 ? 1 : -1;
        int sy = y1 < y2 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            if (x1 >= 0 && x1 < 40 && y1 >= 0 && y1 < 40)
            {
                if (screen[y1, x1] == ' ')
                    screen[y1, x1] = '·';
            }

            if (x1 == x2 && y1 == y2) break;

            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x1 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y1 += sy;
            }
        }
    }
}
