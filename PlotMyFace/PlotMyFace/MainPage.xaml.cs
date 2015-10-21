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
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System.Numerics;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace PlotMyFace
{

    internal class TSPSegment
    {
        public Vector2 start;
        public Vector2 end;
        public enum LineType
        {
            Jump,
            Draw
        };

        public LineType type;
        public bool wasPlotted;

        public TSPSegment()
        {
            wasPlotted = false;
        }

        public uint LineLength()
        {
            double xSeg = (end.X - start.X);
            double ySeg = (end.Y - start.Y);
            return (uint)Math.Abs((Math.Sqrt(xSeg * xSeg + ySeg * ySeg)));
        }

    };

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private const int kJumpThreshold = 5;
        private const int plottingOffsetX = 50;
        private const int plottingOffsetY = 50;
        private const int generationTimeout = 500; // ms
        private const int renderTimeout = 1000; // ms
        private const int maxGenerations = 5;
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

        const int HBotPin_PenServo = 3;    // PWM 3

        const int HBotBed_Width = 350; // mm
        const int HBotBed_Height = 350; // mm

        const float CanvasToRealityScale = 4.0f;

        const int kPenMoveTimeout = 500;


        const int HBotBed_SpindleDiameter = 13; // mm Spindle Diameter
        const int HBotBed_StepsPerRev = 200 * 16; // full steps per rev, 16 microsteps
        const int HBotBed_StepsPerMM = (int)(HBotBed_StepsPerRev / (Math.PI * HBotBed_SpindleDiameter));  // Steps per MM

        private HBot _bot = null;
        private PenActuator _pen = null;

        private MediaCapture _mediaCaptureMgr;
        private StorageFile _photoStorageFile;
        private int _currentGeneration = 0;

        private uint cameraWidth = 0;
        private uint cameraHeight = 0;

        private List<TSPSegment> _tspSegments = new List<TSPSegment>();

        private DispatcherTimer _timer = null;

        public MainPage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {

            base.OnNavigatedTo(e);

            draw.IsEnabled = false;

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(renderTimeout);
            _timer.Tick += _timer_Tick;

            await Task.Run(async () =>
            {
                _bot = new HBot(
                    HBotPin_AStep, HBotPin_ADir, HBotPin_AEn,
                    HBotPin_BStep, HBotPin_BDir, HBotPin_BEn,
                    HBotPin_XHome, HBotPin_YHome);
                _bot.setInfo(HBotBed_Width, HBotBed_Height, HBotBed_StepsPerMM);

                _pen = new PenActuator(HBotPin_PenServo);

                _pen.raise();
                await Task.Delay(kPenMoveTimeout);

                _bot.enable();
                _bot.home();
                while (_bot.run())
                    ;

                _bot.disable();
            });

            _mediaCaptureMgr = new Windows.Media.Capture.MediaCapture();
            await _mediaCaptureMgr.InitializeAsync(new MediaCaptureInitializationSettings { StreamingCaptureMode = StreamingCaptureMode.Video });

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

            cameraWidth = smallestMedia.Width;
            cameraHeight = smallestMedia.Height;
            await _mediaCaptureMgr.VideoDeviceController.SetMediaStreamPropertiesAsync(MediaStreamType.VideoPreview, smallestMedia);

            CapturePreview.Width = cameraWidth;
            CapturePreview.Height = cameraHeight;

            CapturePreview.Source = _mediaCaptureMgr;
            await _mediaCaptureMgr.StartPreviewAsync();

        }

        private void _timer_Tick(object sender, object e)
        {
            lineResult.Invalidate();
        }

        void canvasControl_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (_tspSegments.Count > 0)
            {
                foreach (var line in _tspSegments)
                {
                    if (line.wasPlotted)
                    {
                        args.DrawingSession.DrawLine(line.start * CanvasToRealityScale, line.end * CanvasToRealityScale, Colors.DarkBlue);
                    }
                    else if (line.type == TSPSegment.LineType.Jump)
                    {
                        args.DrawingSession.DrawLine(line.start * CanvasToRealityScale, line.end * CanvasToRealityScale, Colors.YellowGreen);
                    }
                    else if (line.type == TSPSegment.LineType.Draw)
                    {
                        args.DrawingSession.DrawLine(line.start * CanvasToRealityScale, line.end * CanvasToRealityScale, Colors.MediumPurple);
                    }
                }
            }
        }

        private void generateLines()
        {
            Location[] bestSolutionSoFar = _bestSolutionSoFar;
            Location.GetTotalDistance(_startLocation, bestSolutionSoFar);

            _tspSegments.Clear();

            if (bestSolutionSoFar.Length > 0)
            {
                var actualLocation = _startLocation;
                int index = 0;
                int dropCount = 0;

                foreach (var destination in _AddEndLocation(bestSolutionSoFar))
                {
                    var line = new TSPSegment() { start = new Vector2(actualLocation.X, actualLocation.Y), end = new Vector2(destination.X, destination.Y) };
                    if (line.LineLength() > kJumpThreshold)
                    {
                        line.type = TSPSegment.LineType.Jump;
                    }
                    else
                    {
                        line.type = TSPSegment.LineType.Draw;
                    }

                    _tspSegments.Add(line);

                    actualLocation = destination;
                    index++;
                }

                Debug.WriteLine("Total long jumps: " + dropCount);
                Debug.WriteLine("Segments added: " + _tspSegments.Count);
            }
        }

        private IEnumerable<Location> _AddEndLocation(Location[] middleLocations)
        {
            foreach (var location in middleLocations)
                yield return location;

            yield return _startLocation;
        }

        private async void draw_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            capture.IsEnabled = false;
            draw.IsEnabled = false;
            if (_bestSolutionSoFar.Length == 0)
            {
                capture.IsEnabled = true;
                return;
            }

            // Reset the plotted bit on segments in case someone draws the same image again.
            foreach (var line in _tspSegments)
            {
                line.wasPlotted = false;
            }

            _timer.Start();

            await Task.Run(async () =>
            {
                var start = DateTime.Now;
                _pen.raise();
                await Task.Delay(kPenMoveTimeout);

                _bot.move((int)(_tspSegments[0].start.X + plottingOffsetX), (int)(_tspSegments[0].start.Y + plottingOffsetY));

                _bot.enable();
                while (_bot.run())
                    ;
                _bot.disable();

                TSPSegment.LineType currentType = _tspSegments[0].type;
                if (currentType == TSPSegment.LineType.Draw)
                {
                    _pen.lower();
                    await Task.Delay(kPenMoveTimeout);
                }

                foreach (var line in _tspSegments)
                {
                    if (line.type != currentType)
                    {
                        if (line.type == TSPSegment.LineType.Draw)
                        {
                            _pen.lower();
                            await Task.Delay(kPenMoveTimeout);
                        }
                        else
                        {
                            _pen.raise();
                            await Task.Delay(kPenMoveTimeout);
                        }

                        currentType = line.type;
                    }

                    _bot.move((int)(line.end.X + plottingOffsetX), (int)(line.end.Y + plottingOffsetY));
                    line.wasPlotted = true;

                    _bot.enable();
                    while (_bot.run())
                        ;
                    _bot.disable();

                }

                Debug.WriteLine("Render took: " + (DateTime.Now - start).TotalSeconds.ToString() + " seconds");

                _bot.home();
                _bot.enable();
                while (_bot.run())
                    ;
                _bot.disable();

                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    draw.IsEnabled = true;
                    capture.IsEnabled = true;
                    _timer.Stop();
                });
            });
        }

        private async void capture_Click(object sender, RoutedEventArgs e)
        {
            _photoStorageFile = await Windows.Storage.ApplicationData.Current.LocalFolder.CreateFileAsync(PHOTO_FILE_NAME, Windows.Storage.CreationCollisionOption.GenerateUniqueName);

            ImageEncodingProperties imageProperties = ImageEncodingProperties.CreateJpeg();
            imageProperties.Width = cameraWidth;
            imageProperties.Height = cameraHeight;

            await _mediaCaptureMgr.CapturePhotoToStorageFileAsync(imageProperties, _photoStorageFile);

            await Task.Run(async () =>
            {
                var ditherResult = await Dither.ditherFile(_photoStorageFile, imageProperties.Width, imageProperties.Height);

                _locations = Linearization.GetLocationsFromDither(ditherResult.pixels, (int)ditherResult.width, (int)ditherResult.height);

                if (_locations.Count > 0)
                {
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
                    _algorithm.MustMutateFailedCrossovers = true;
                    _algorithm.MustDoCrossovers = true;


                    while (_currentGeneration++ < maxGenerations)
                    {
                        _algorithm.Reproduce();
                    }

                    _bestSolutionSoFar = _algorithm.GetBestSolutionSoFar().ToArray();
                    Debug.WriteLine("TSP took: " + (DateTime.Now - start).TotalSeconds.ToString() + " seconds");

                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        draw.IsEnabled = true;
                    });
                }

                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    var startLines = DateTime.Now;
                    generateLines();
                    lineResult.Invalidate();
                    Debug.WriteLine("LineDraw " + (DateTime.Now - startLines).TotalSeconds.ToString() + " seconds");
                });
            });
        }
    }
}
