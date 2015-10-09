using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Media.Capture;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace PlotMyFace
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private const int maxGenerations = 100;
        private const int _populationCount = 114;
        Location _startLocation = new Location(50, 50);
        TravellingSalesmanAlgorithm _algorithm;
        List<Location> _locations;
        private Location[] _bestSolutionSoFar;

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

            StorageFile file;
            if (true)
            {
                // Use the camera image grab
                file = await dialog.CaptureFileAsync(CameraCaptureUIMode.Photo);
            }
            else
            {
                // use a file
                FileOpenPicker openPicker = new FileOpenPicker();
                openPicker.ViewMode = PickerViewMode.Thumbnail;
                openPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                openPicker.FileTypeFilter.Add(".jpg");
                openPicker.FileTypeFilter.Add(".jpeg");
                openPicker.FileTypeFilter.Add(".png");
                file = await openPicker.PickSingleFileAsync();
            }

            if (file == null)
            {
                return;
            }

            var ditherResult = await Dither.ditherFile(file, 150, 200);

            _locations = Linearization.GetLocationsFromDither(ditherResult.pixels, (int)ditherResult.width, (int)ditherResult.height);

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

            await Task.Delay(500); // To get a paint frame
            Debug.WriteLine("TSP Start: " + DateTime.Now.ToString());
            _algorithm = new TravellingSalesmanAlgorithm(_startLocation, _locations.ToArray(), _populationCount);
            _bestSolutionSoFar = _algorithm.GetBestSolutionSoFar().ToArray();
            Debug.WriteLine("TSP End: " + DateTime.Now.ToString());

            Debug.WriteLine("LineDraw Start: " + DateTime.Now.ToString());
            _DrawLines();
            Debug.WriteLine("LineDraw End: " + DateTime.Now.ToString());
        }

        uint LineLength(Line line)
        {
            double xSeg = (line.X2 - line.X1);
            double ySeg = (line.Y2 - line.Y1);
            return (uint)Math.Abs((Math.Sqrt(xSeg * xSeg + ySeg * ySeg)));
        }

        private void _DrawLines()
        {
            Location[] bestSolutionSoFar = _bestSolutionSoFar;
            Location.GetTotalDistance(_startLocation, bestSolutionSoFar);

            var canvasChildren = lineResult.Children;
            canvasChildren.Clear();

            var actualLocation = _startLocation;
            int index = 0;
            var color = Colors.Purple;
            var stroke = new SolidColorBrush(color);
            int dropCount = 0;
            foreach (var destination in _AddEndLocation(bestSolutionSoFar))
            {
                int red = 255 * index / bestSolutionSoFar.Length;
                int blue = 255 - red;

                var line = new Line();
                line.Stroke = stroke;
                line.X1 = actualLocation.X;
                line.Y1 = actualLocation.Y;
                line.X2 = destination.X;
                line.Y2 = destination.Y;
                uint lineLength = LineLength(line);
                if (lineLength > 10)
                {
                    Debug.WriteLine("Dropping line length: " + lineLength);
                    dropCount++;
                }
                else
                {
                    canvasChildren.Add(line);
                }

                actualLocation = destination;
                index++;
            }

            Debug.WriteLine("Total dropped: " + dropCount);

        }
        private IEnumerable<Location> _AddEndLocation(Location[] middleLocations)
        {
            foreach (var location in middleLocations)
                yield return location;

            yield return _startLocation;
        }
    }
}
