using System;
using System.Collections;
using System.IO;
using alphaShot;
using BepInEx;
using Illusion.Game;
using Manager;
using UnityEngine;

namespace ImageSeriesRecorder
{
    [BepInPlugin("marco.ImageSeriesRecorder", "Image Series Recorder", "1.0")]
    [BepInDependency("com.bepis.bepinex.screenshotmanager")]
    [BepInDependency("com.bepis.bepinex.configurationmanager")]
    public class ImageSeriesRecorder : BaseUnityPlugin
    {
        private const int InputFieldWidth = 70;
        private Rect _windowRect;
        private string _lastMessage;

        public bool DisplayingWindow { get; private set; }
        public SavedKeyboardShortcut KeyboardShortcut { get; private set; }

        private ConfigWrapper<bool> _alphaCapture;
        private ConfigWrapper<string> _captureDirectory;
        private ConfigWrapper<int> _downscaleRate;
        private ConfigWrapper<int> _resolutionH;
        private ConfigWrapper<int> _resolutionW;
        private ConfigWrapper<int> _secondsToRecord;
        private ConfigWrapper<int> _frameRate;

        private bool _isStudio;
        private bool _noCtrlConditionDone;

        private int _originalFramerate;
        private int _originalVsync;

        private int FrameCountToRecord => _secondsToRecord.Value * _frameRate.Value;
        private int _frameCounter;
        private bool _skipping;
        private bool _gatheringFps;

        private float _deltaTime;
        private float _minFps;
        private int _skipFrames;
        private int _currentSkipFrame;

        private AlphaShot2 _screenGrabber;
        
        private static AlphaShot2 GetSceenshotHandler()
        {
            if (Camera.main != null)
            {
                var alphaShot2 = Camera.main.gameObject.GetComponent<AlphaShot2>();
                if (alphaShot2 != null)
                    return alphaShot2;
                return Camera.main.gameObject.AddComponent<AlphaShot2>();
            }
            return null;
        }

        private bool IsCaptureRunning()
        {
            return _screenGrabber != null;
        }

        private void OnGUI()
        {
            if (DisplayingWindow)
                _windowRect = GUILayout.Window(568, _windowRect, Window, "Image Series Recorder");
        }

        private void Start()
        {
            _isStudio = Application.productName == "CharaStudio";

            _alphaCapture = new ConfigWrapper<bool>(nameof(_alphaCapture), this);
            _captureDirectory = new ConfigWrapper<string>(nameof(_captureDirectory), this, Path.Combine(Application.persistentDataPath, "capture"));
            _downscaleRate = new ConfigWrapper<int>(nameof(_downscaleRate), this, 1);
            _resolutionH = new ConfigWrapper<int>(nameof(_resolutionH), this, Screen.height);
            _resolutionW = new ConfigWrapper<int>(nameof(_resolutionW), this, Screen.width);
            _secondsToRecord = new ConfigWrapper<int>(nameof(_secondsToRecord), this, 5);
            _frameRate = new ConfigWrapper<int>(nameof(_frameRate), this, 25);

            KeyboardShortcut = new SavedKeyboardShortcut("Open recorder window", this, new KeyboardShortcut(KeyCode.R, KeyCode.LeftShift));

            _windowRect = new Rect(200, 200, 350, 200);
            _lastMessage = "Ready";
        }

        private void StartCapture()
        {
            _lastMessage = "Preparing to start...";

            _screenGrabber = GetSceenshotHandler();

            // Need to limit fps to the framerate to match recording speed
            _originalFramerate = Application.targetFrameRate;
            _originalVsync = QualitySettings.vSyncCount;
            QualitySettings.vSyncCount = 0;
            _frameCounter = 0;
            _skipping = true;

            _gatheringFps = true;
            Application.targetFrameRate = 0;
            StartCoroutine(GetherFpsCo());
        }

        private IEnumerator GetherFpsCo()
        {
            yield return new WaitForSeconds(3);

            Application.targetFrameRate = _frameRate.Value;

            _skipFrames = (int)(_frameRate.Value / _minFps) + 1;
            _gatheringFps = false;
        }

        private void StopCapture()
        {
            _screenGrabber = null;
            Time.timeScale = 1;
            Application.targetFrameRate = _originalFramerate;
            QualitySettings.vSyncCount = _originalVsync;

            Utils.Sound.Play(SystemSE.ok_l);

            _lastMessage = "Finished saving images to " + _captureDirectory.Value;
        }

        private void Update()
        {
            if (_isStudio && KeyboardShortcut.IsDown() && !Scene.Instance.IsNowLoadingFade && Singleton<StudioScene>.Instance)
            {
                DisplayingWindow = !DisplayingWindow;

                _minFps = float.MaxValue;

                if (!_noCtrlConditionDone)
                {
                    var oldCondition = Studio.Studio.Instance.cameraCtrl.noCtrlCondition;
                    Studio.Studio.Instance.cameraCtrl.noCtrlCondition = () => IsMouseOverWindow() || oldCondition();
                    _noCtrlConditionDone = true;
                }
            }

            if (DisplayingWindow)
            {
                if (_gatheringFps)
                {
                    _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;
                    float fps = 1.0f / _deltaTime;
                    if (fps < _minFps) _minFps = fps;
                }
                else if (IsCaptureRunning())
                {
                    if (Input.GetKey(KeyCode.LeftShift))
                    {
                        StopCapture();
                        return;
                    }

                    if (!_skipping)
                    {
                        // Only 0 stops fixedupdate
                        Time.timeScale = 0;

                        _lastMessage = $"Capturing frame {_frameCounter} / {FrameCountToRecord}";
                        
                        var result = _screenGrabber.Capture(_resolutionW.Value, _resolutionH.Value, _downscaleRate.Value, _alphaCapture.Value);

                        File.WriteAllBytes(Path.Combine(_captureDirectory.Value, $@"{_frameCounter:D5}.png"), result);

                        if (_frameCounter++ > FrameCountToRecord)
                            StopCapture();
                        else
                            _skipping = true;
                    }
                    else
                    {
                        if (Time.timeScale > float.Epsilon)
                        {
                            // Need to run 1 frame at practically stopped time to avoid time from jumping forward to compensate because we locked the thread for a long time
                            Time.timeScale = float.Epsilon;
                        }
                        else
                        {
                            // Compensate for the game running slower than target framerate
                            Time.timeScale = (1 - float.Epsilon) / _skipFrames;

                            if (++_currentSkipFrame >= _skipFrames)
                            {
                                _currentSkipFrame = 0;
                                _skipping = false;
                            }
                        }
                    }
                }
            }
        }

        private bool IsMouseOverWindow()
        {
            return DisplayingWindow && _windowRect.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y));
        }

        private void Window(int id)
        {
            if (!IsCaptureRunning())
            {
                GUILayout.BeginVertical(GUI.skin.box);
                {
                    GUILayout.Label("Capture settings", GUILayout.ExpandWidth(true));

                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label("Seconds to record", GUILayout.ExpandWidth(true));
                        if (int.TryParse(GUILayout.TextField(_secondsToRecord.Value.ToString(), GUILayout.Width(InputFieldWidth)), out var secondsToRecord))
                            _secondsToRecord.Value = secondsToRecord;
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label("Frame rate (FPS)", GUILayout.ExpandWidth(true));
                        if (int.TryParse(GUILayout.TextField(_frameRate.Value.ToString(), GUILayout.Width(InputFieldWidth)), out var frameRate))
                            _frameRate.Value = frameRate;
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label("Width", GUILayout.ExpandWidth(true));
                        if (int.TryParse(GUILayout.TextField(_resolutionW.Value.ToString(), GUILayout.Width(InputFieldWidth)), out var resolutionW))
                            _resolutionW.Value = resolutionW;
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label("Height", GUILayout.ExpandWidth(true));
                        if (int.TryParse(GUILayout.TextField(_resolutionH.Value.ToString(), GUILayout.Width(InputFieldWidth)), out var resolutionH))
                            _resolutionH.Value = resolutionH;
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label("Upsampling rate (Slow!)", GUILayout.ExpandWidth(true));
                        if (int.TryParse(GUILayout.TextField(_downscaleRate.Value.ToString(), GUILayout.Width(InputFieldWidth)), out var downscaleRate))
                            _downscaleRate.Value = downscaleRate;
                    }
                    GUILayout.EndHorizontal();

                    _alphaCapture.Value = GUILayout.Toggle(_alphaCapture.Value, "Capture transparency");

                    _captureDirectory.Value = GUILayout.TextField(_captureDirectory.Value, GUILayout.ExpandWidth(true), GUILayout.MaxWidth(350));
                }
                GUILayout.EndVertical();

                GUILayout.BeginHorizontal(GUI.skin.box);
                {
                    if (GUILayout.Button("Start"))
                    {
                        try
                        {
                            var directoryInfo = Directory.CreateDirectory(_captureDirectory.Value);
                            if (directoryInfo.Exists)
                            {
                                _captureDirectory.Value = directoryInfo.FullName;
                                StartCapture();
                            }
                            else
                                throw new IOException("Failed to create directory");
                        }
                        catch (Exception e)
                        {
                            _lastMessage = "Failed to start recording - " + e.Message;
                        }
                    }
                }
                GUILayout.EndHorizontal();

            }
            else
            {
                GUILayout.Label("To stop capturing prematurely hold Left Shift. \n\n" +
                                "Avoid doing anything that could lag the game or the frames might become out of sync!", GUILayout.MaxWidth(350));
                GUILayout.Space(20);
            }

            GUILayout.Label(_lastMessage, GUILayout.MaxWidth(350));

            GUI.DragWindow();
        }
    }
}
