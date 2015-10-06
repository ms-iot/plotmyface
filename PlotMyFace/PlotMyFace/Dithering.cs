using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Media.Capture;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Storage.FileProperties;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;

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
        const byte kBWThreshold = 96;
        internal static async Task<DitherResult> ditherFile(StorageFile file, uint outputWidth = 150, uint outputHeight = 200)
        {
            DitherResult result = new DitherResult();
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

                // First Pass, average each triad, then clip to threshold.
                for (int i = 0; i < thumbnailBWArray.Length; i++)
                {
                    thumbnailBWArray[i] = (byte)((thumbnailColorArray[i * 4 + 0] + thumbnailColorArray[i * 4 + 1] + thumbnailColorArray[i * 4 + 2]) / 3);

                    if (thumbnailBWArray[i] < kBWThreshold)
                    {
                        thumbnailDitheredArray[i] = (byte)0;
                    }
                    else
                    {
                        thumbnailDitheredArray[i] = (byte)255;
                    }
                }

                for (int x = 0; x < result.width; x++)
                {
                    for (int y = 0; y < result.height; y++)
                    {
                        int index = y * (int)result.width + x;
                        int error = thumbnailDitheredArray[index] - thumbnailBWArray[index];

                        if (x + 1 < result.width)
                        {
                            thumbnailDitheredArray[index + 1] = (byte)(thumbnailDitheredArray[index + 1] + (error * 7) / 16);
                        }

                        if (y + 1 < result.height)
                        {
                            if (x - 1 > 0)
                            {
                                thumbnailDitheredArray[index + result.width - 1] = (byte)(thumbnailDitheredArray[index + result.width - 1] + (error * 3) / 16);
                            }

                            thumbnailDitheredArray[index + result.width] = (byte)(thumbnailDitheredArray[index + result.width] + (error * 5) / 16);

                            if (x + 1 < result.width)
                            {
                                thumbnailDitheredArray[index + result.width + 1] = (byte)(thumbnailDitheredArray[index + result.width + 1] + (error * 1) / 16);
                            }
                        }
                    }
                }

                //thumbnailDitheredArray now contains the black and white dither.
                result.pixels = thumbnailDitheredArray;
                return result;
            }
        }
    }
}
