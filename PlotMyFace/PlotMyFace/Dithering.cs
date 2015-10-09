using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace PlotMyFace
{
    public struct DitherResult
    {
        public uint width;
        public uint height;
        public byte[] pixels;
    }

    internal class Dither
    {
        const byte kBWThreshold = 0;
        static public float[] errorTable;

        internal static byte getLuminance(byte r, byte g, byte b)
        {
            byte Max = Math.Max(r, Math.Max(g, b));
            byte Min = Math.Min(r, Math.Min(g, b));
            return (byte)((Max + Min) / 2);
        }
        enum dithermode { Sierra2, SierraLite, FloydSteinberg};


        internal static async Task<DitherResult> ditherFile(StorageFile file, uint outputWidth = 150, uint outputHeight = 200)
        {
            int pointCount = 0;
            dithermode dm = dithermode.SierraLite;
            DitherResult result = new DitherResult();
            errorTable = new float[outputWidth * outputHeight];

            using (IRandomAccessStream stream = await file.OpenReadAsync())
            {

                var decoder = await BitmapDecoder.CreateAsync(stream);

                var bitmapTransform = new BitmapTransform
                {
                    ScaledWidth = outputWidth,
                    ScaledHeight = outputHeight
                };

                var thumbnailColorProvider = await decoder.GetPixelDataAsync(
                                BitmapPixelFormat.Rgba8,
                                BitmapAlphaMode.Straight,
                                bitmapTransform,
                                ExifOrientationMode.IgnoreExifOrientation,
                                ColorManagementMode.DoNotColorManage);

                var thumbnailColorArray = thumbnailColorProvider.DetachPixelData();

                result.width = outputWidth;
                result.height = outputHeight;

                var thumbnailBWArray = new byte[result.width * result.height];
                var thumbnailDitheredArray = new byte[result.width * result.height];

                // First Pass, average each triad 
                for (int i = 0; i < thumbnailBWArray.Length; i++)
                {
                    var r = thumbnailColorArray[i * 4 + 0];
                    var g = thumbnailColorArray[i * 4 + 1];
                    var b = thumbnailColorArray[i * 4 + 2];

                    thumbnailBWArray[i] = getLuminance(r, g, b);
                }

                int newValue;
                float error;
                for (var pass = 0; pass < 1; pass++)
                {
                    for (int x = 0; x < result.width; x++)
                    {
                        for (int y = 0; y < result.height; y++)
                        {
                            int index = y * (int)result.width + x;

                            newValue = thumbnailBWArray[index] + (int)errorTable[index];
                            
                            if (newValue >= kBWThreshold)
                            {
                                // Set white
                                error = newValue - 255;
                                thumbnailDitheredArray[index] = 255;
                            }
                            else
                            {
                                // Set Black
                                pointCount++;
                                error = newValue;
                                thumbnailDitheredArray[index] = 0;
                            }

                            switch (dm)
                            {
                                case dithermode.FloydSteinberg:
                                    {
                                        if (x + 1 < result.width)
                                        {
                                            errorTable[index + 1] += (error * 7 / 16);
                                        }

                                        if (y + 1 < result.height)
                                        {
                                            if (x - 1 >= 0)
                                            {
                                                errorTable[index + result.width - 1] += (error * 3 / 16);
                                            }

                                            errorTable[index + result.width] += (error * 5 / 16);

                                            if (x + 1 < result.width)
                                            {
                                                errorTable[index + result.width + 1] += (error * 1 / 16);
                                            }
                                        }
                                    }
                                    break;
                                case dithermode.Sierra2:
                                    {
                                        if (x + 1 < result.width)
                                        {
                                            errorTable[index + 1] += (error * 4 / 16);
                                        }
                                        if (x + 2 < result.width)
                                        {
                                            errorTable[index + 2] += (error * 3 / 16);
                                        }

                                        if (y + 1 < result.height)
                                        {
                                            if (x - 2 >= 0)
                                            {
                                                errorTable[index + result.width - 2] += (error * 1 / 16);
                                            }
                                            if (x - 1 >= 0)
                                            {
                                                errorTable[index + result.width - 1] += (error * 2 / 16);
                                            }


                                            errorTable[index + result.width] += (error * 3 / 16);

                                            if (x + 1 < result.width)
                                            {
                                                errorTable[index + result.width + 1] += (error * 2 / 16);
                                            }

                                            if (x + 2 < result.width)
                                            {
                                                errorTable[index + result.width + 2] += (error * 1 / 16);
                                            }
                                        }

                                    }
                                    break;

                                case dithermode.SierraLite:
                                    {
                                        if (x + 1 < result.width)
                                        {
                                            errorTable[index + 1] += (error * 2 / 4);
                                        }

                                        if (y + 1 < result.height)
                                        {
                                            if (x - 1 >= 0)
                                            {
                                                errorTable[index + result.width - 1] += (error * 1 / 4);
                                            }

                                            errorTable[index + result.width] += (error * 1 / 4);

                                        }
                                    }
                                    break;
                            }

                        }
                    }
                }
                Debug.WriteLine("Point count: " + pointCount);

                result.pixels =  thumbnailDitheredArray;

                return result;
            }
        }
    }
}
