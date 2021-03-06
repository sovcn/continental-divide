﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ContinentalDivide
{

    class LocationStack
    {
        private Location top;
        private int N = 0;

        public void push(Location l)
        {
            l.next = this.top;
            this.top = l;
            this.N++;
        }

        public Location pop()
        {
            Location l = this.top;
            this.top = l.next;
            this.N--;
            return l;
        }
    }

    class Location
    {

        public enum Adjacent
        {
            Left,
            Up,
            Right,
            Down
        }

        public int x { get; set; }
        public int y { get; set; }

        public short elevation { get; set; }

        public Location next { get; set; } // Allows this to be used in a linked list or stack

        public bool isCoast = false;

        public Location GetAdjacent(Location[,] map, Adjacent dir)
        {
            switch (dir)
            {
                case Adjacent.Left:
                    if( x > 1)
                    {
                        return map[y, x - 1];
                    }
                    break;
                case Adjacent.Up:
                    if( y > 1)
                    {
                        return map[y - 1, x];
                    }
                    break;
                case Adjacent.Right:
                    if( x < map.GetLength(1) - 1)
                    {
                        return map[y, x + 1];
                    }
                    break;
                case Adjacent.Down:
                    if( y < map.GetLength(0) - 1)
                    {
                        return map[y + 1, x];
                    }
                    break;
                default:
                    return null;
            }

            return null;
        }
    }

    class Program
    {
        const int NUM_COLUMNS = 10800;

        static Location[,] readElevationMap()
        {
            var isLittleEndian = BitConverter.IsLittleEndian;

            byte[] elevationInput;
            Location[,] elevationMap;

            if (isLittleEndian)
            {
                elevationInput = File.ReadAllBytes(@"data/e10g");
                if (elevationInput.Length % 2 != 0)
                {
                    throw new ArgumentException("Expected an even number of bytes for 16bit input.");
                }
                int N = elevationInput.Length / 2;
                if (N % NUM_COLUMNS != 0)
                {
                    throw new ArgumentException("Expected the number of 16bit entries to be evenly divisible by the number of columns which is constant (10800).");
                }

                int numRows = N / NUM_COLUMNS; // 16-bit columns, not 8bit columns
                int rowByteSize = NUM_COLUMNS * 2;

                elevationMap = new Location[numRows, NUM_COLUMNS];
                for( var i=0; i < numRows; i++)
                {
                    for(var j = 0; j < NUM_COLUMNS; j++)
                    {
                        elevationMap[i, j] = new Location();
                    }
                }

                for (var i = 0; i < numRows; i++)
                {
                    for (var j = 0; j < NUM_COLUMNS * 2; j += 2) // Double # of 16-bit rows to address each 8bit input, step by 16bits at a time
                    {
                        var firstByte = i * rowByteSize + j;

                        var loc = elevationMap[i, j / 2];

                        loc.elevation = BitConverter.ToInt16(elevationInput, firstByte);
                        loc.x = j / 2;
                        loc.y = i;

                    }
                }

                return elevationMap;
            }
            else
            {
                throw new PlatformNotSupportedException("Running on Big Endian architectures is not currently supported.");
            }
        }


        static void DrawElevationMapImage(string folder, Location[,] elevationMap)
        {
            var width = elevationMap.GetLength(1); // Num columns = width
            var height = elevationMap.GetLength(0); // Num rows = height
            int bpp = 4;


            byte[] imageMap = new byte[width * height * bpp]; // 4 bytes per pixel

            short max = 0;
            for (var j = 0; j < height; j++) // each row
            {
                for (var i = 0; i < width; i++) // each column of each row
                {
                    if( elevationMap[j, i].elevation > max)
                    {
                        max = elevationMap[j, i].elevation;
                    }
                }
            }


            for (var j = 0; j < height; j++) // each row
            {
                for (var i = 0; i < width; i++) // each column of each row
                {
                    var index = (width * bpp * j) + (i * bpp);

                    var loc = elevationMap[j, i];
                    short elevation = loc.elevation;

                    if ( elevation > 0 )
                    {
                        if( loc.isCoast)
                        {
                            imageMap[index] = 0; // Blue
                            imageMap[index + 1] = 255; // Green
                            imageMap[index + 2] = 0; // Red
                            imageMap[index + 3] = 255; // Alpha
                        }
                        else
                        {
                            var grey = (byte)Math.Floor(elevation * 1.0 / max * 255.0);
                            imageMap[index] = grey;
                            imageMap[index + 1] = grey;
                            imageMap[index + 2] = grey;
                            imageMap[index + 3] = 255;
                        }
                    }
                    else
                    {
                        imageMap[index] = 0;
                        imageMap[index + 1] = 0;
                        imageMap[index + 2] = 0;
                        imageMap[index + 3] = 255;
                    }
                }
            }

            var arrayHandle = GCHandle.Alloc(imageMap, GCHandleType.Pinned);
            var bmp = new Bitmap(width, height,
                width*bpp, // bytes per row
                PixelFormat.Format32bppArgb,
                arrayHandle.AddrOfPinnedObject()
            );

            bmp.Save(@"data/test.bmp");

            arrayHandle.Free(); // Free the pinned memory handle so GC can clear things up.  Prevents a memory leak.
        }



        static void CalculateCoast(Location[,] elevationMap, LocationStack stack)
        {
            // 1. Push all coastal tiles onto stack.
            var width = elevationMap.GetLength(1); // Num Columns
            var height = elevationMap.GetLength(0); // Num Rows

            Location loc;

            // TODO : This needs to instead do a breadth-first search from two starting seeds to determine drainage basins for two bodies of water

            for (var j = 0; j < height; j++) // For each row, scan from outside in and push first coast tile on stack
            {
                for(var i = 0; i<width; i++)
                {
                    loc = elevationMap[j, i];
                    if( loc.elevation >= 0)
                    {
                        var left = loc.GetAdjacent(elevationMap, Location.Adjacent.Left);
                        var up = loc.GetAdjacent(elevationMap, Location.Adjacent.Up);
                        var right = loc.GetAdjacent(elevationMap, Location.Adjacent.Right);
                        var down = loc.GetAdjacent(elevationMap, Location.Adjacent.Down);

                        if ( ( left != null && left.elevation < 0 ) 
                            || (up != null && up.elevation < 0)
                            || (right != null && right.elevation < 0)
                            || (down != null && down.elevation < 0)
                        )
                        {
                            loc.isCoast = true;
                            stack.push(loc);
                        }
                    }
                }
            }
        }

        static void Main(string[] args)
        {

            Location[,] elevationMap = readElevationMap();

            var stack = new LocationStack();

            // 1. Seed the search algorithm with all coast tiles
            CalculateCoast(elevationMap, stack);

            // 2. For each item on the stack, push all upwards neighbors on stack and repeat
                // Mark each item as accessible from a particular ocean (how...)

            // TODO

            // 3. Output image of resulting calculations
            DrawElevationMapImage(@"data/", elevationMap);

            var numNotOcean = 0;
            foreach(var loc in elevationMap)
            {
                if( loc.elevation != -500)
                {
                    numNotOcean++;
                }
            }

            Console.WriteLine("Testing");
        }
    }
}
