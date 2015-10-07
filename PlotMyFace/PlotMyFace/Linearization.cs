using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlotMyFace
{
    class Linearization
    {
        public static List<Location> GetLocationsFromDither(byte[] pixels, int width, int height)
        {
            var locations = new List<Location>();

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    var index = y * width + x;
                    byte value = pixels[index];
                    if (value < 96)
                    {
                        locations.Add(new Location(x, y));
                    }
                }
            }

            return locations;
        }
    }
}
