using DirectShowLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

// Started with the code posted here: https://stackoverflow.com/a/18189027/206
// Cloned from https://github.com/anotherlab/FocusUF to add other camera properties

namespace FocusUF
{
    public enum Operation
    {
        Usage = 0,
        ManualFocus = 1,
        AutoFocus = 2,
        ManualExposure = 3,
        AutoExposure = 4,
        SetFocus = 5,
        SetExposure = 6,
        ListCameras = 7,
        Contrast = 8,
        Saturation = 9,
        Brightness = 10,
        Gamma = 11,
        ManualWhiteBalance = 12,
        AutoWhiteBalance = 13,
        WhiteBalance = 14,
        NULL =- 1
    }

    class Program
    {
        // Global argument values
        static Operation _whatToDo = Operation.Usage;
        static string _cameraName = "USB Camera"; // Assuming only one cam plugged in
        static int _focusSetting;
        static int _exposureSetting;
        static int _contrastSetting;
        static int _saturationSetting;
        static int _brightnessSetting;
        static int _gammaSetting;
        static int _whiteBalanceSetting;

        static void Main(string[] args)
        {
            Console.WriteLine("FocusUF-ng " + Assembly.GetEntryAssembly().GetName().Version);
            Console.WriteLine("Get the source at https://github.com/gpapp/FocusUF");

            var argsLists = SplitArgs(args.ToList());

            // Get the list of connected video cameras
            DsDevice[] devs = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);

            foreach (var argsList in argsLists)
            {
                _whatToDo = ProcessArgs(argsList);
                IAMCameraControl camera;
                IAMVideoProcAmp videoProcAmp;
                switch (_whatToDo)
                {
                    case Operation.Usage:
                        Usage();
                        break;

                    case Operation.ListCameras:
                        foreach (var cam in devs)
                        {
                            Console.WriteLine($"Camera: {cam.Name}");
                            camera = GetCamera(cam);
                            videoProcAmp = GetVideoProcAmp(cam);
                            if (camera != null)
                            {
                                // Focus ranges and Values
                                camera.GetRange(CameraControlProperty.Focus, out int focusMin, out int focusMax, out int focusStep, out int focusDefault, out CameraControlFlags focusPossFlags);
                                camera.Get(CameraControlProperty.Focus, out int focusValue, out CameraControlFlags focusSetting);
                                Console.WriteLine($"    Focus Capability: {focusPossFlags}");
                                Console.WriteLine($"    Focus Range: {focusMin} - {focusMax}");
                                Console.WriteLine($"    Focus Setting: {focusSetting}, {focusValue}");
                                camera.GetRange(CameraControlProperty.Exposure, out int expMin, out int expMax, out int expStep, out int expDefault, out CameraControlFlags expPossFlags);
                                camera.Get(CameraControlProperty.Exposure, out int expValue, out CameraControlFlags expSetting);
                                Console.WriteLine($"    Exposure Capability: {expPossFlags}");
                                Console.WriteLine($"    Exposure Range: {expMin} - {expMax}");
                                Console.WriteLine($"    Exposure Setting: {expSetting}, {expValue}");
                                videoProcAmp.GetRange(VideoProcAmpProperty.WhiteBalance, out int wbMin, out int wbMax, out int wbStep, out int wbDefault, out VideoProcAmpFlags wbCapsFlags);
                                videoProcAmp.Get(VideoProcAmpProperty.WhiteBalance, out int wbValue, out VideoProcAmpFlags wbSetting);
                                Console.WriteLine($"    WhiteBalance Capability: {wbCapsFlags}");
                                Console.WriteLine($"    WhiteBalance Range: {wbMin} - {wbMax}");
                                Console.WriteLine($"    WhiteBalance Setting: {wbSetting}, {wbValue}");
                            }
                            else
                            {
                                Console.WriteLine($"    Camera does not expose settings through DirectShowLib");
                            }
                        }
                        break;

                    case Operation.ManualFocus:
                        SetCameraFlag(devs, _cameraName, CameraControlProperty.Focus, CameraControlFlags.Manual);
                        break;

                    case Operation.AutoFocus:
                        SetCameraFlag(devs, _cameraName, CameraControlProperty.Focus, CameraControlFlags.Auto);
                        break;

                    case Operation.SetFocus:
                        SetCameraValue(devs, _cameraName, CameraControlProperty.Focus, _focusSetting);
                        break;

                    case Operation.ManualExposure:
                        SetCameraFlag(devs, _cameraName, CameraControlProperty.Exposure, CameraControlFlags.Manual);
                        break;

                    case Operation.AutoExposure:
                        SetCameraFlag(devs, _cameraName, CameraControlProperty.Exposure, CameraControlFlags.Auto);
                        break;

                    case Operation.SetExposure:
                        SetCameraValue(devs, _cameraName, CameraControlProperty.Exposure, _exposureSetting);
                        break;

                    case Operation.Saturation:
                        SetVideoProcValue(devs, _cameraName, VideoProcAmpProperty.Saturation, _saturationSetting);
                        break;

                    case Operation.Gamma:
                        SetVideoProcValue(devs, _cameraName, VideoProcAmpProperty.Gamma, _gammaSetting);
                        break;

                    case Operation.Brightness:
                        SetVideoProcValue(devs, _cameraName, VideoProcAmpProperty.Brightness, _brightnessSetting);
                        break;

                    case Operation.Contrast:
                        SetVideoProcValue(devs, _cameraName, VideoProcAmpProperty.Contrast, _contrastSetting);
                        break;

                    case Operation.ManualWhiteBalance:
                        SetVideoProcAmpFlags(devs, _cameraName, VideoProcAmpProperty.WhiteBalance, VideoProcAmpFlags.Manual);
                        break;

                    case Operation.AutoWhiteBalance:
                        SetVideoProcAmpFlags(devs, _cameraName, VideoProcAmpProperty.WhiteBalance, VideoProcAmpFlags.Auto);
                        break;

                    case Operation.WhiteBalance:
                        SetVideoProcValue(devs, _cameraName, VideoProcAmpProperty.WhiteBalance, _whiteBalanceSetting);
                        break;

                }
            }

        }

        /// <summary>
        /// Gets a camera inteface we can use from the generic device
        /// </summary>
        /// <param name="dev"></param>
        /// <returns></returns>
        static IAMCameraControl GetCamera(DsDevice dev)
        {
            IAMCameraControl _camera = null;
            if (dev != null)
            {
                // DirectShow uses a module system called filters to exposure the functionality
                // We create a new object that implements the IFilterGraph2 interface so that we can
                // new filters to exposure the functionality that we need.
                if (new FilterGraph() is IFilterGraph2 graphBuilder)
                {
                    // Create a video capture filter for the device
                    graphBuilder.AddSourceFilterForMoniker(dev.Mon, null, dev.Name, out IBaseFilter capFilter);

                    // Cast that filter to IAMCameraControl from the DirectShowLib
                    _camera = capFilter as IAMCameraControl;
                }
            }
            return _camera;
        }
        /// <summary>
        /// Gets a video processor inteface we can use from the generic device
        /// </summary>
        /// <param name="dev"></param>
        /// <returns></returns>
        static IAMVideoProcAmp GetVideoProcAmp(DsDevice dev)
        {
            IAMVideoProcAmp _camera = dev as IAMVideoProcAmp;
            if (dev != null)
            {
                // DirectShow uses a module system called filters to exposure the functionality
                // We create a new object that implements the IFilterGraph2 interface so that we can
                // new filters to exposure the functionality that we need.
                if (new FilterGraph() is IFilterGraph2 graphBuilder)
                {
                    // Create a video capture filter for the device
                    graphBuilder.AddSourceFilterForMoniker(dev.Mon, null, dev.Name, out IBaseFilter capFilter);

                    // Cast that filter to IAMCameraControl from the DirectShowLib
                    _camera = capFilter as IAMVideoProcAmp;
                }
            }
            return _camera;
        }

        static void SetCameraFlag (DsDevice[] devs, string cameraName, CameraControlProperty camProperty, CameraControlFlags flagVal)
        {
            IAMCameraControl camera = GetCamera(devs.Where(d => d.Name.ToLower().Contains(cameraName.ToLower())).FirstOrDefault());

            if (camera != null)
            {
                // Get the current settings from the webcam
                camera.Get(camProperty, out int v, out CameraControlFlags f);

                // If the camera property differs from the desired value, adjust it leaving value the same.
                if (f != flagVal)
                {
                    camera.Set(camProperty, v, flagVal);
                    Console.WriteLine($"{cameraName} {camProperty} set to {flagVal}");
                }
                else
                {
                    Console.WriteLine($"{cameraName} {camProperty} already {flagVal}");
                }
            }
            else
            {
                Console.WriteLine($"No physical camera matching \"{cameraName}\" found");
            }
        }

        static void SetCameraValue(DsDevice[] devs, string cameraName, CameraControlProperty camProperty, int val)
        {
            IAMCameraControl camera = GetCamera(devs.Where(d => d.Name.ToLower().Contains(cameraName.ToLower())).FirstOrDefault());

            if (camera != null)
            {
                // Get the current settings from the webcam
                camera.Get(camProperty, out int v, out CameraControlFlags f);

                // If the camera value differs from the desired value, adjust it leaving flag the same.
                if (v != val)
                {
                    camera.Set(camProperty, val, f);
                    Console.WriteLine($"{cameraName} {camProperty} value set to {val}");
                }
                else
                {
                    Console.WriteLine($"{cameraName} {camProperty} value already {val}");
                }
            }
            else
            {
                Console.WriteLine($"No physical camera matching \"{cameraName}\" found");
            }
        }

        static void SetVideoProcAmpFlags(DsDevice[] devs, string cameraName, VideoProcAmpProperty videoProperty, VideoProcAmpFlags flagVal)
        {
            IAMVideoProcAmp videoProcAmp = GetVideoProcAmp(devs.Where(d => d.Name.ToLower().Contains(cameraName.ToLower())).FirstOrDefault());

            if (videoProcAmp != null)
            {
                // Get the current settings from the webcam
                videoProcAmp.Get(videoProperty, out int v, out VideoProcAmpFlags f);

                // If the camera value differs from the desired value, adjust it leaving flag the same.
                if (f != flagVal)
                {
                    videoProcAmp.Set(videoProperty, v, flagVal);
                    Console.WriteLine($"{cameraName} {videoProperty} value set to {flagVal}");
                }
                else
                {
                    Console.WriteLine($"{cameraName} {videoProperty} value already {flagVal}");
                }
            }
            else
            {
                Console.WriteLine($"No physical camera matching \"{cameraName}\" found");
            }
        }

        static void SetVideoProcValue(DsDevice[] devs, string cameraName, VideoProcAmpProperty property, int val)
        {
            IAMVideoProcAmp videoProcAmp =  GetVideoProcAmp(devs.Where(d => d.Name.ToLower().Contains(cameraName.ToLower())).FirstOrDefault());
            videoProcAmp.GetRange(property, out int min, out int max, out int steppingDelta,  out int defaultValue, out var flags);

            Console.WriteLine($"min: {min}, max: {max}, steppingDelta: {steppingDelta} defaultValue: {defaultValue}, flags: {flags}");
            val = Math.Min(val, max);
            val = Math.Max(val, min);
            if (videoProcAmp != null)
            {
                // Get the current settings from the webcam
                videoProcAmp.Get(property, out int v, out VideoProcAmpFlags f);

                // If the camera value differs from the desired value, adjust it leaving flag the same.
                if (v != val)
                {
                    videoProcAmp.Set(property, val, f);
                    Console.WriteLine($"{cameraName} {property} value set to {val}");
                }
                else
                {
                    Console.WriteLine($"{cameraName} {property} value already {val}");
                }
                videoProcAmp.Get(property, out int v2, out VideoProcAmpFlags f2);
                Console.WriteLine($"Setting {property} value {(val==v2?"successful":"failed")} {cameraName} {property} value is at {v2}");
            }
            else
            {
                Console.WriteLine($"No physical camera matching \"{cameraName}\" found");
            }
        }

        /// <summary>
        /// Split the argument list on "--and" into multiple lists
        /// </summary>
        /// <param name="args">The raw program arguments</param>
        /// <returns>A list of sublists of arguments</returns>
        static IList<List<string>> SplitArgs(List<string> args)
        {
            var argLists = new List<List<string>>();
            for (var startIndex = 0; startIndex < args.Count; )
            {
                var andIndex = args.FindIndex(startIndex, s => s.Equals("--and", StringComparison.OrdinalIgnoreCase));
                if (andIndex < 0)
                    andIndex = args.Count;
                argLists.Add(args.GetRange(startIndex, andIndex - startIndex));
                startIndex = andIndex + 1;
            }
            return argLists;
        }

        /// <summary>
        /// Basic argument processing, return an operation and set globals
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        static Operation ProcessArgs(List<string> args)
        {
            if (args.Contains("--help") || args.Contains("-?"))
                return Operation.Usage;

            if (args.Contains("--list-cameras") || args.Contains("-l"))
                return Operation.ListCameras;

            var nameArgIx = args.IndexOf("--camera-name");
            nameArgIx = nameArgIx != -1 ? nameArgIx : args.IndexOf("-n");
            if (nameArgIx != -1 && args.Count >= nameArgIx + 2)
            {
                _cameraName = args[nameArgIx + 1];
            }

            if (args.Contains("--focus-mode-manual") || args.Contains("-fm"))
                return Operation.ManualFocus;

            if (args.Contains("--focus-mode-auto") || args.Contains("-fa"))
                return Operation.AutoFocus;

            if (args.Contains("--exposure-mode-manual") || args.Contains("-em"))
                return Operation.ManualExposure;

            if (args.Contains("--exposure-mode-auto") || args.Contains("-ea"))
                return Operation.AutoExposure;

            if (args.Contains("--whitebalance-mode-manual") || args.Contains("-wbm"))
                return Operation.ManualWhiteBalance;

            if (args.Contains("--whitebalance-mode-auto") || args.Contains("-wba"))
                return Operation.AutoWhiteBalance;

            var focusSetArgIx = args.IndexOf("--set-focus");
            focusSetArgIx = focusSetArgIx != -1 ? focusSetArgIx : args.IndexOf("-f");
            if (focusSetArgIx != -1 && args.Count >= focusSetArgIx + 2 && int.TryParse(args[focusSetArgIx + 1], out int focusVal))
            {
                _focusSetting = focusVal;
                return Operation.SetFocus;
            }

            var exposureSetArgIx = args.IndexOf("--set-exposure");
            exposureSetArgIx = exposureSetArgIx != -1 ? exposureSetArgIx : args.IndexOf("-e");
            if (exposureSetArgIx != -1 && args.Count >= exposureSetArgIx + 2 && int.TryParse(args[exposureSetArgIx + 1], out int expVal))
            {
                _exposureSetting = expVal;
                return Operation.SetExposure;
            }

            Int32 argVal;
            if ((argVal = processArgument(args,"brightness", "b"))!=int.MinValue) {
                _brightnessSetting= argVal;
                return Operation.Brightness;
            }
            if ((argVal = processArgument(args, "contrast", "c")) != int.MinValue)
            {
                _contrastSetting = argVal;
                return Operation.Contrast;
            }
            if ((argVal = processArgument(args, "saturation", "s")) != int.MinValue)
            {
                _saturationSetting = argVal;
                return Operation.Saturation;
            }
            if ((argVal = processArgument(args, "gamma", "g")) != int.MinValue)
            {
                _gammaSetting = argVal;
                return Operation.Gamma;
            }
            if ((argVal = processArgument(args, "whitebalance", "wb")) != int.MinValue)
            {
                _whiteBalanceSetting = argVal;
                return Operation.WhiteBalance;
            }

            return Operation.Usage;
        }
        private static int processArgument(List<string> args, String longArg, String shortArg)
        {
            var argIx = args.IndexOf("--set-" + longArg);
            argIx = argIx != -1 ? argIx : args.IndexOf("-" + shortArg);
            if (argIx != -1 && args.Count >= argIx + 2 && int.TryParse(args[argIx + 1], out int expVal))
            {
                return expVal;
            }
            return int.MinValue;
        }

        static void Usage()
        {
            Console.WriteLine(@"
Usage: FocusUF [--help | -?] [--list-cameras | -l]
               [--focus-mode-manual | -fm] [--focus-mode-auto | -fa]
               [--set-focus <value> | -f <value>]
               [--exposure-mode-manual | -em] [--exposure-mode-auto | -ea]
               [--set-exposure <value> | -e <value>]
               [--set-brightness <value> | -b <value>]
               [--set-contrast <value> | -c <value>]
               [--set-saturation <value> | -s <value>]
               [--set-gamma <value> | -g <value>]
               [--whitebalance-mode-manual | -wbm] [--whitebalance-mode-auto | -wba]
               [--set-whitebalance <value> | -wb <value>]
               [--camera-name <name> | -n <name>]
               [--and {more operations...}]");
        }

    }
}
