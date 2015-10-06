using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Media.Capture;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Storage.FileProperties;
using Windows.UI.Xaml.Media.Imaging;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace PlotMyFace
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {

            base.OnNavigatedTo(e);

            CameraCaptureUI dialog = new CameraCaptureUI();
            Size aspectRatio = new Size(8, 10);
            dialog.PhotoSettings.CroppedAspectRatio = aspectRatio;
            dialog.PhotoSettings.Format = CameraCaptureUIPhotoFormat.Jpeg;

            StorageFile file = await dialog.CaptureFileAsync(CameraCaptureUIMode.Photo);

            var ditherResult = await Dither.ditherFile(file);

            MemoryStream ms = new MemoryStream(ditherResult.pixels);

            var expand = new byte[ditherResult.width * ditherResult.height * 4];
            for (int i = 0; i < ditherResult.pixels.Length; i++)
            {
                expand[i * 4 + 0] = ditherResult.pixels[i];
                expand[i * 4 + 1] = ditherResult.pixels[i];
                expand[i * 4 + 2] = ditherResult.pixels[i];
                expand[i * 4 + 3] = 255;
            }

            WriteableBitmap bi = new WriteableBitmap((int)ditherResult.width, (int)ditherResult.height);
            expand.CopyTo(bi.PixelBuffer);

            ditheredImageResult.Source = bi;
        }
    }
}
