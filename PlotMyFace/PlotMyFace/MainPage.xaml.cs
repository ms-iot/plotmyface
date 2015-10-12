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
using MachineInterface;
using Windows.Media.MediaProperties;
using Windows.UI.Xaml;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace PlotMyFace
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private const int generationTimeout = 500; // ms
        private const int maxGenerations = 100;
        private const int _populationCount = 114;
        Location _startLocation = new Location(50, 50);
        TravellingSalesmanAlgorithm _algorithm;
        List<Location> _locations;
        private Location[] _bestSolutionSoFar;

        private readonly String PHOTO_FILE_NAME = "Capture.jpg";

        const int HBotPin_AStep = 16;    // GPIO_23;
        const int HBotPin_ADir = 12;     // GPIO_18;
        const int HBotPin_AEn = 18;   // GPIO_24;

        const int HBotPin_BStep = 31;    // GPIO_6;
        const int HBotPin_BDir = 33;   // GPIO_13;
        const int HBotPin_BEn = 29;     //GPIO_5;

        const int HBotPin_XHome = 38;   //GPIO_19;
        const int HBotPin_YHome = 35;   //GPIO_20;

        const int HBotBed_Width = 350; // mm
        const int HBotBed_Height = 350; // mm

        const int HBotBed_SpindleDiameter = 13; // mm Spindle Diameter
        const int HBotBed_StepsPerRev = 200 * 16; // full steps per rev, 16 microsteps
        const int HBotBed_StepsPerMM = (int)(HBotBed_StepsPerRev / (Math.PI * HBotBed_SpindleDiameter));  // Steps per MM

        private HBot _bot = null;

        private DispatcherTimer _timer = null;
        private MediaCapture _mediaCaptureMgr;
        private StorageFile _photoStorageFile;
        private int _currentGeneration = 0;

        public MainPage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {

            base.OnNavigatedTo(e);

            draw.IsEnabled = false;
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(generationTimeout);
            _timer.Tick += _timer_Tick;

            _mediaCaptureMgr = new Windows.Media.Capture.MediaCapture();
            await _mediaCaptureMgr.InitializeAsync(new MediaCaptureInitializationSettings { StreamingCaptureMode = StreamingCaptureMode.Video });

            if (_mediaCaptureMgr.MediaCaptureSettings.VideoDeviceId != "" && _mediaCaptureMgr.MediaCaptureSettings.AudioDeviceId != "")
            {
                _mediaCaptureMgr.Failed += new Windows.Media.Capture.MediaCaptureFailedEventHandler(CameraFailedHandler);
            }
            else
            {
                Debug.WriteLine("No VideoDevice/AudioDevice Found");
            }

            VideoEncodingProperties smallestMedia = null;

            var cameraProperties = _mediaCaptureMgr.VideoDeviceController.GetAvailableMediaStreamProperties(MediaStreamType.VideoPreview).Select(x => x as VideoEncodingProperties).ToList();
            if (cameraProperties.Count >= 1)
            {
                foreach (var mediaEncodingProperty in cameraProperties)
                {
                    if (smallestMedia == null ||
                        (smallestMedia.Width > mediaEncodingProperty.Width ||
                        smallestMedia.Height > mediaEncodingProperty.Height))
                    {
                        smallestMedia = mediaEncodingProperty;
                    }
                }
            }

            await _mediaCaptureMgr.VideoDeviceController.SetMediaStreamPropertiesAsync(MediaStreamType.VideoPreview, smallestMedia);

            // for testing
            /*
            if (false)
            {
                // use a file
                FileOpenPicker openPicker = new FileOpenPicker();
                openPicker.ViewMode = PickerViewMode.Thumbnail;
                openPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                openPicker.FileTypeFilter.Add(".jpg");
                openPicker.FileTypeFilter.Add(".jpeg");
                openPicker.FileTypeFilter.Add(".png");
                _photoStorageFile = await openPicker.PickSingleFileAsync();
            }
            */

            CapturePreview.Source = _mediaCaptureMgr;
            await _mediaCaptureMgr.StartPreviewAsync();

            await Task.Run(() =>
            {
                _bot = new HBot(
                    HBotPin_AStep, HBotPin_ADir, HBotPin_AEn,
                    HBotPin_BStep, HBotPin_BDir, HBotPin_BEn,
                    HBotPin_XHome, HBotPin_YHome);
                _bot.setInfo(HBotBed_Width, HBotBed_Height, HBotBed_StepsPerMM);

                _bot.enable();
                _bot.home();
                while (_bot.run())
                    ;
                _bot.disable();
            });
        }

        public async void CameraFailedHandler(MediaCapture currentCaptureObject, MediaCaptureFailedEventArgs currentFailure)
        {
            try
            {
                // Notify the user
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    Debug.WriteLine("Fatal error" + currentFailure.Message);
                });
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }
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
            Polygon poly = new Polygon();
            poly.Stroke = stroke;
            poly.StrokeThickness = 1;

            foreach (var destination in _AddEndLocation(bestSolutionSoFar))
            {
                int red = 255 * index / bestSolutionSoFar.Length;
                int blue = 255 - red;

                var line = new Line();
                var point = new Point();

                line.Stroke = stroke;
                line.X1 = actualLocation.X;
                line.Y1 = actualLocation.Y;
                line.X2 = destination.X;
                line.Y2 = destination.Y;

                point.X = destination.X;
                point.Y = destination.Y;

                uint lineLength = LineLength(line);
                if (lineLength > 10)
                {
                    Debug.WriteLine("Dropping line length: " + lineLength);
                    dropCount++;
                }
                else
                {
                    poly.Points.Add(point);
                    //canvasChildren.Add(line);
                }

                actualLocation = destination;
                index++;
            }
            canvasChildren.Add(poly);

            Debug.WriteLine("Total long dropped: " + dropCount);
            Debug.WriteLine("Poly added: " + canvasChildren.Count);
        }
        private IEnumerable<Location> _AddEndLocation(Location[] middleLocations)
        {
            foreach (var location in middleLocations)
                yield return location;

            yield return _startLocation;
        }

        private async void draw_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            draw.IsEnabled = false;
            await Task.Run(async () =>
            {
                Location[] bestSolutionSoFar = _bestSolutionSoFar;
                Location.GetTotalDistance(_startLocation, bestSolutionSoFar);

                var actualLocation = _startLocation;
                int index = 0;
                var start = DateTime.Now;
                foreach (var destination in _AddEndLocation(bestSolutionSoFar))
                {
                    _bot.move(destination.X + 20, destination.Y + 20);

                    _bot.enable();
                    while (_bot.run())
                        ;
                    _bot.disable();

                    actualLocation = destination;
                    index++;
                }

                Debug.WriteLine("Render took: " + (DateTime.Now - start).TotalSeconds.ToString() + " seconds");

                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    draw.IsEnabled = true;
                });
            });
        }

        private void Stop_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
        }

        private async void button_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();

            _photoStorageFile = await Windows.Storage.ApplicationData.Current.LocalFolder.CreateFileAsync(PHOTO_FILE_NAME, Windows.Storage.CreationCollisionOption.GenerateUniqueName);

            ImageEncodingProperties imageProperties = ImageEncodingProperties.CreateJpeg();

            await _mediaCaptureMgr.CapturePhotoToStorageFileAsync(imageProperties, _photoStorageFile);

            await Task.Run(async () =>
            {
                var ditherResult = await Dither.ditherFile(_photoStorageFile);

                _locations = Linearization.GetLocationsFromDither(ditherResult.pixels, (int)ditherResult.width, (int)ditherResult.height);

                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
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
                });

                var start = DateTime.Now;

                _algorithm = new TravellingSalesmanAlgorithm(_startLocation, _locations.ToArray(), _populationCount);
                _bestSolutionSoFar = _algorithm.GetBestSolutionSoFar().ToArray();
                Debug.WriteLine("TSP took: " + (DateTime.Now - start).TotalSeconds.ToString() + " seconds");

                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    draw.IsEnabled = true;
                });

                /*
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    var startLines = DateTime.Now;
                    _DrawLines();
                    Debug.WriteLine("LineDraw " + (DateTime.Now - startLines).TotalSeconds.ToString() + " seconds");
                });
                */
            });

            //_currentGeneration = 0;
            //_timer.Start();
        }

        private void _timer_Tick(object sender, object e)
        {
            if (_currentGeneration++ > maxGenerations)
            {
                _timer.Stop();
            }
            else if (_algorithm != null)
            {
                _algorithm.MustMutateFailedCrossovers = true;
                _algorithm.MustDoCrossovers = true;
                _algorithm.Reproduce();
                _bestSolutionSoFar = _algorithm.GetBestSolutionSoFar().ToArray();
                _DrawLines();
            }
        }
    }
}
