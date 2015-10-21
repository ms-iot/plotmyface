# Plot My Face using Travelling Salesperson Art
This project demonstrates Direct Memory Mapping and Hybrid applications which leverage Arduino libraries on Windows 10 IoT Core for Raspberry Pi. 

The key elements of this project are:

* Using camera to capture an image, with a live preview
* Using Xaml UI to display intermediate stages of the plot
* Dither the input image to generate a series of dots
* Use a travelling salesperson algorithm to connect the the dots using line segments
* Use the line segements to drive an H-Bot style carteisian plotter
* Drive steppers using the Arduino library [AccelStepper](http://www.airspayce.com/mikem/arduino/AccelStepper/) written by Mike McCauley.

# Bill of Materials
* Raspberry Pi 2
* [MakeBlock Lab Kit](http://www.makeblock.cc/lab-robot-kit-blue-no-electronics/) (or equivelent parts)
* [MakeBlock Long Rails](http://www.makeblock.cc/beam0824-496-blue-6-pack/)
* 2x [SparkFun Big Easy Driver](https://www.sparkfun.com/products/12859)
* (optional) [Adafruit permaproto HAT for Raspberry Pi 2](http://www.adafruit.com/products/2310)
* Video Camera which supports USB Video Class driver.
* 3d Printed Idle Wheel

# Software Components
* Windows 10 IoT Core, build 10556 or later
* [Win2D for Windows 10](https://github.com/Microsoft/win2d)
* [AccelStepper](http://www.airspayce.com/mikem/arduino/AccelStepper/)
* [Travelling Salesman Algorithm](http://www.codeproject.com/Articles/792887/Travelling-Salesman-Genetic-Algorithm)

# Explaination of the hardware build
The hardware for this plotter is configured as an H-Bot cartesian frame. An HBot uses a single long toothed belt and two stepper motors to drive the X and Y axes. The configuration has the benefit that both of the the stepper motors are mounted to the frame, which means the central carriage is very light and can move exceptionally fast. The downside of the HBot it is not balanced - if the Y axis is not extremely rigid, there can be racking. The simplicity of this design makes it a quick and easy build.


## Steppers
The two steppers work together to drive the X and Y axes, instead of each motor controlling a single axis. The formula for converting from axis steps to multi-motor steps are:

```aStepper = xStep + yStep```
```bStepper = xStep - yStep```

# Software
The application that drives this project is a hybrid application - it uses a C# and Xaml host, with a C++/CX Windows component which hosts the Arduino wiring code.

## Capture
The Capture pipeline uses MediaCapture in the Windows.Media.Capture namespace. This component is associated with a live preview window in xaml. When the user selects capture, a picture is saved to storage then processing begins.

## Dither
The image is then processed using a Floyd-Steinberg Two-dimensional error diffusion dithering algorithm. It is currently configured to reduce the number of points for speed purposes. This can be controled by adjusting ```kBWThreshold``` in ```Dithering.cs```

## Travelling Salesperson
The points in the dither are connected using a Travelling Salesperson Algorithm - generating [TSP Art](http://wiki.evilmadscientist.com/TSP_art). The agorithm is currently configured for expedient rendering, not an optimal solution. If you want to reconfigure this, instructs are available in ```TravellingSalesmanAlgorithm.cs```

## Drawing
Once the TSP is generated, the [Win2D](https://github.com/Microsoft/win2d) canvas draws the TSP, with progress. The datastructure backing the rendering is used to generate the toolpath - and ultimately the stepper profiles using [AccelStepper](http://www.airspayce.com/mikem/arduino/AccelStepper/).

# left as an exercise to the reader
There are many things that can be done with this project:
* Convert the H-Bot to [CoreXY](http://corexy.com/) which balances the load better
* Use a [rigid plotter frame](http://www.makeblock.cc/xy-plotter-robot-kit/)
* [Laser Engraving](http://www.makeblock.cc/laser-engraver-upgrade-pack-500mw-for-xy-plotter-robot-kit-v2-0/)
* Parallelize dithering and TSP generation


