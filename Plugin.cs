using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.IO;
using System.Reflection;
using MU3.Mecha;
using MU3.DB;

namespace InputMonitorMod
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger = null!;

        static string listenAddr = "http://127.0.0.1:9716/";
        
        static InputState currentState = null!;
        static InputState exportedState = null!;
        
        private System.Net.HttpListener httpListener = null!;
        private Thread listenerThread = null!;
        private bool isRunning = false;

        private void Awake()
        {
            Logger = base.Logger;
            Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        }

        private void Start()
        {
            Harmony.CreateAndPatchAll(typeof(Plugin));
            Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} Started!");

            currentState = new InputState();
            exportedState = new InputState();

            StartHttpServer();
        }

        private void OnDestroy()
        {
            StopHttpServer();
        }

        private void Update()
        {
            Jvs jvs = MechaManager.jvs;
            if (jvs == null) return;

            currentState.UpdateFromJvs(jvs);

            if (!currentState.Equals(exportedState))
            {
                lock (exportedState)
                {
                    exportedState.CopyFrom(currentState);
                    Monitor.Pulse(exportedState);
                }
            }
        }

        private void StartHttpServer()
        {
            try
            {
                httpListener = new System.Net.HttpListener();
                httpListener.Prefixes.Add(listenAddr);
                httpListener.Start();
                isRunning = true;

                listenerThread = new Thread(HandleRequests);
                listenerThread.IsBackground = true;
                listenerThread.Start();

                Logger.LogInfo($"HTTP Server started on {listenAddr}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to start HTTP server: {ex.Message}");
            }
        }

        private void StopHttpServer()
        {
            isRunning = false;
            if (httpListener != null && httpListener.IsListening)
            {
                httpListener.Stop();
                httpListener.Close();
            }
            if (listenerThread != null && listenerThread.IsAlive)
            {
                listenerThread.Join(1000);
            }
        }

        private void HandleRequests()
        {
            while (isRunning)
            {
                try
                {
                    var context = httpListener.GetContext();
                    var request = context.Request;
                    var response = context.Response;

                    response.Headers.Add("Access-Control-Allow-Origin", "*");
                    response.Headers.Add("Access-Control-Allow-Methods", "GET, OPTIONS");
                    response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

                    if (request.HttpMethod == "OPTIONS")
                    {
                        response.StatusCode = 200;
                        response.Close();
                        continue;
                    }

                    if (request.Url.AbsolutePath == "/state")
                    {
                        string responseString;
                        lock (exportedState)
                        {
                            responseString = exportedState.ToJson();
                        }
                        response.ContentType = "application/json";
                        byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                        response.ContentLength64 = buffer.Length;
                        response.OutputStream.Write(buffer, 0, buffer.Length);
                        response.OutputStream.Close();
                    }
                    else if (request.Url.AbsolutePath == "/")
                    {
                        string responseString = GetHtmlPage();
                        response.ContentType = "text/html";
                        byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                        response.ContentLength64 = buffer.Length;
                        response.OutputStream.Write(buffer, 0, buffer.Length);
                        response.OutputStream.Close();
                    }
                    else if (request.Url.AbsolutePath.StartsWith("/images/"))
                    {
                        ServeStaticFile(request, response);
                    }
                    else
                    {
                        response.StatusCode = 404;
                        string responseString = "Not Found";
                        byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                        response.ContentLength64 = buffer.Length;
                        response.OutputStream.Write(buffer, 0, buffer.Length);
                        response.OutputStream.Close();
                    }
                }
                catch (Exception ex)
                {
                    if (isRunning)
                    {
                        Logger.LogError($"Error handling request: {ex.Message}");
                    }
                }
            }
        }

        private void ServeStaticFile(System.Net.HttpListenerRequest request, System.Net.HttpListenerResponse response)
        {
            try
            {
                string pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string relativePath = request.Url.AbsolutePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                string filePath = Path.Combine(pluginDir, relativePath);
                
                if (!File.Exists(filePath))
                {
                    Logger.LogWarning($"File not found: {filePath}");
                    response.StatusCode = 404;
                    response.Close();
                    return;
                }
                
                byte[] fileBytes = File.ReadAllBytes(filePath);
                string extension = Path.GetExtension(filePath).ToLower();
                switch (extension)
                {
                    case ".png":
                        response.ContentType = "image/png";
                        break;
                    case ".jpg":
                    case ".jpeg":
                        response.ContentType = "image/jpeg";
                        break;
                    case ".gif":
                        response.ContentType = "image/gif";
                        break;
                    case ".svg":
                        response.ContentType = "image/svg+xml";
                        break;
                    default:
                        response.ContentType = "application/octet-stream";
                        break;
                }
                
                response.ContentLength64 = fileBytes.Length;
                response.StatusCode = 200;
                response.OutputStream.Write(fileBytes, 0, fileBytes.Length);
                response.OutputStream.Close();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error serving file: {ex.Message}");
                response.StatusCode = 500;
                response.Close();
            }
        }

        private string GetHtmlPage()
        {
            return @"<!DOCTYPE html>
<html lang='zh-CN'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>ONGEKI Button Printer</title>
    <style>
        * {
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }
        
        body {
            background: transparent;
            font-family: 'Arial', 'Microsoft YaHei', sans-serif;
            display: flex;
            justify-content: center;
            align-items: center;
            min-height: 100vh;
            overflow: hidden;
        }
        
        /* 主控制器容器 */
        #controller-container {
            position: relative;
            width: 600px;
            height: 800px;
            border: none;
            box-shadow: none;
            overflow: hidden;
            background: transparent;
        }
        
        /* 等待背景图片 - 作为底层 */
        .background-image {
            position: absolute;
            top: 0;
            left: 0;
            width: 100%;
            height: 100%;
            background-image: url('images/buttons/waiting.png');
            background-size: contain;
            background-position: center;
            background-repeat: no-repeat;
            z-index: 1;
            pointer-events: none;
        }
        
        /* 按钮容器 */
        #buttons-container {
            position: absolute;
            top: 0;
            left: 0;
            width: 100%;
            height: 100%;
            z-index: 2;
        }
        
        /* 动态生成的按钮图片 */
        .dynamic-button {
            position: absolute;
            width: 600px;
            height: 800px;
            object-fit: contain;
            transition: none !important;
            will-change: opacity;
            backface-visibility: hidden;
            transform: translateZ(0);
        }
        
        .dynamic-button.hidden {
            opacity: 0;
            transform: scale(0.95);
            pointer-events: none;
        }
        
        .dynamic-button.visible {
            opacity: 1;
            transform: scale(1);
        }
        
        .z-buttons { z-index: 3; }
        .z-swing { z-index: 99; }
    </style>
</head>
<body>
    <div id='controller-container'>
        <div class='background-image'></div>
        <div id='buttons-container'></div>
    </div>

    <script>
        const BUTTONS_DATA = [
            { key: '1_on', image_url: 'images/buttons/1_on.png' },
            { key: '2_on', image_url: 'images/buttons/2_on.png' },
            { key: '3_on', image_url: 'images/buttons/3_on.png' },
            { key: '4_on', image_url: 'images/buttons/4_on.png' },
            { key: '5_on', image_url: 'images/buttons/5_on.png' },
            { key: '6_on', image_url: 'images/buttons/6_on.png' },
            { key: '7_on', image_url: 'images/buttons/7_on.png' },
            { key: '8_on', image_url: 'images/buttons/8_on.png' },
            { key: '1_motion', image_url: 'images/buttons/1.png' },
            { key: '2_motion', image_url: 'images/buttons/2.png' },
            { key: '3_motion', image_url: 'images/buttons/3.png' },
            { key: '4_motion', image_url: 'images/buttons/4.png' },
            { key: '5_motion', image_url: 'images/buttons/5.png' },
            { key: '6_motion', image_url: 'images/buttons/6.png' },
            { key: '7_motion', image_url: 'images/buttons/7.png' },
            { key: '8_motion', image_url: 'images/buttons/8.png' },
            { key: 'l_lever_-2', image_url: 'images/buttons/l_swing_-2.png' },
            { key: 'l_lever_-1', image_url: 'images/buttons/l_swing_-1.png' },
            { key: 'l_lever_0', image_url: 'images/buttons/l_swing_0.png' },
            { key: 'l_lever_1', image_url: 'images/buttons/l_swing_1.png' },
            { key: 'l_lever_2', image_url: 'images/buttons/l_swing_2.png' },
            { key: 'r_lever_-2', image_url: 'images/buttons/r_swing_-2.png' },
            { key: 'r_lever_-1', image_url: 'images/buttons/r_swing_-1.png' },
            { key: 'r_lever_0', image_url: 'images/buttons/r_swing_0.png' },
            { key: 'r_lever_1', image_url: 'images/buttons/r_swing_1.png' },
            { key: 'r_lever_2', image_url: 'images/buttons/r_swing_2.png' },
            { key: 'swing_-2', image_url: 'images/buttons/swing_-2.png' },
            { key: 'swing_-1', image_url: 'images/buttons/swing_-1.png' },
            { key: 'swing_0', image_url: 'images/buttons/swing_0.png' },
            { key: 'swing_1', image_url: 'images/buttons/swing_1.png' },
            { key: 'swing_2', image_url: 'images/buttons/swing_2.png' },
            { key: 'rest_l', image_url: 'images/buttons/l_0.png' },
            { key: 'rest_r', image_url: 'images/buttons/r_0.png' }
        ];
        
        console.log('[Init] Loading button images...');
        
        const buttonMapping = {
            'LeftWall': '1_on',
            'Left1': '2_on',
            'Left2': '3_on',
            'Left3': '4_on',
            'Right1': '5_on',
            'Right2': '6_on',
            'Right3': '7_on',
            'RightWall': '8_on'
        };
        
        const images = new Map();
        const buttonsContainer = document.getElementById('buttons-container');
        const swingKeys = new Set([
            'l_lever_-2', 'l_lever_-1', 'l_lever_0', 'l_lever_1', 'l_lever_2',
            'r_lever_-2', 'r_lever_-1', 'r_lever_0', 'r_lever_1', 'r_lever_2',
            'swing_-2', 'swing_-1', 'swing_0', 'swing_1', 'swing_2'
        ]);
        
        const leftButtons = new Set(['LeftWall', 'Left1', 'Left2', 'Left3']);
        const rightButtons = new Set(['Right1', 'Right2', 'Right3', 'RightWall']);
        BUTTONS_DATA.forEach(button => {
            const img = document.createElement('img');
            img.src = button.image_url;
            img.setAttribute('data-key', button.key);
            img.className = 'dynamic-button hidden';
            img.alt = button.key;
            
            if (swingKeys.has(button.key)) {
                img.classList.add('z-swing');
            } else {
                img.classList.add('z-buttons');
            }
            
            buttonsContainer.appendChild(img);
            images.set(button.key, img);
        });
        
        console.log('[Init] Created', images.size, 'button images');
        
        let lastLeverKey = '0';
        let lastLeverPos = 0;
        let lastSubPos = 0;
        let leverStableCount = 0;
        let firstUpdate = true;
        let preferLeft = false;
        
        let leftPressedButtons = [];
        let rightPressedButtons = [];
        let lastLeftMotion = null;
        let lastRightMotion = null;
        let previousButtonStates = {};
        let showRestLeft = false;
        let showRestRight = false;
        
        function getLeverKey(leverValue) {
            if (leverValue < -0.6) return '-2';
            if (leverValue < -0.2) return '-1';
            if (leverValue > 0.6) return '2';
            if (leverValue > 0.2) return '1';
            return '0';
        }
        
        function hideAllLeverImages(leverKey) {
            const lImg = images.get('l_lever_' + leverKey);
            const rImg = images.get('r_lever_' + leverKey);
            const sImg = images.get('swing_' + leverKey);
            if (lImg) { lImg.classList.remove('visible'); lImg.classList.add('hidden'); }
            if (rImg) { rImg.classList.remove('visible'); rImg.classList.add('hidden'); }
            if (sImg) { sImg.classList.remove('visible'); sImg.classList.add('hidden'); }
        }
        
        const initialRightLeverImg = images.get('r_lever_0');
        const initialRestLeft = images.get('rest_l');
        if (initialRightLeverImg) {
            console.log('[Init] Showing initial r_lever_0');
            initialRightLeverImg.classList.remove('hidden');
            initialRightLeverImg.classList.add('visible');
        } else {
            console.error('[Init] Initial r_lever_0 image not found!');
        }
        if (initialRestLeft) {
            console.log('[Init] Showing initial rest_l');
            initialRestLeft.classList.remove('hidden');
            initialRestLeft.classList.add('visible');
        } else {
            console.error('[Init] Initial rest_l image not found!');
        }
        
        function updateDisplay(data) {
            if (firstUpdate) {
                console.log('[First Update] Received data:', data);
                for (const btnName of Object.keys(buttonMapping)) {
                    previousButtonStates[btnName] = false;
                }
                firstUpdate = false;
            }
            
            for (const [btnName, imgKey] of Object.entries(buttonMapping)) {
                const isPressed = data.buttons[btnName];
                const wasPressed = previousButtonStates[btnName];
                const isLeft = leftButtons.has(btnName);
                
                if (isPressed && !wasPressed) {
                    handleButtonPress(btnName, imgKey, isLeft);
                }
                else if (!isPressed && wasPressed) {
                    handleButtonRelease(btnName, imgKey, isLeft);
                }
                
                const btnImg = images.get(imgKey);
                if (btnImg) {
                    if (isPressed) {
                        btnImg.classList.remove('hidden');
                        btnImg.classList.add('visible');
                    } else {
                        btnImg.classList.remove('visible');
                        btnImg.classList.add('hidden');
                    }
                }
                
                previousButtonStates[btnName] = isPressed;
            }
            
            const hasLeftButtons = leftPressedButtons.length > 0;
            const hasRightButtons = rightPressedButtons.length > 0;
            
            const leverKey = getLeverKey(data.lever.value);
            const currentLeverPos = data.lever.raw;
            const currentSubPos = data.lever.direct;
            
            if (currentLeverPos !== lastLeverPos) {
                console.log('[Lever] Position changed from', lastLeverPos, 'to', currentLeverPos);
                hideAllLeverImages(lastLeverKey);
                lastLeverKey = leverKey;
                lastLeverPos = currentLeverPos;
                leverStableCount = 0;
            } else if (currentSubPos === lastSubPos) {
                leverStableCount++;
            } else {
                leverStableCount = 0;
            }
            lastSubPos = currentSubPos;
            
            hideAllLeverImages(leverKey);
            const restLeft = images.get('rest_l');
            const restRight = images.get('rest_r');
            if (restLeft) { restLeft.classList.remove('visible'); restLeft.classList.add('hidden'); }
            if (restRight) { restRight.classList.remove('visible'); restRight.classList.add('hidden'); }
            
            const isLeverReleased = leverStableCount > 30;
            
            if (hasLeftButtons && hasRightButtons) {
                const swingImg = images.get('swing_' + leverKey);
                if (swingImg) {
                    swingImg.classList.remove('hidden');
                    swingImg.classList.add('visible');
                }
                showRestLeft = false;
                showRestRight = false;
            } else if (hasLeftButtons && !hasRightButtons) {
                if (isLeverReleased) {
                    const swingImg = images.get('swing_' + leverKey);
                    if (swingImg) {
                        swingImg.classList.remove('hidden');
                        swingImg.classList.add('visible');
                    }
                    showRestLeft = false;
                    showRestRight = true;
                } else {
                    const lImg = images.get('l_lever_' + leverKey);
                    if (lImg) {
                        lImg.classList.remove('hidden');
                        lImg.classList.add('visible');
                    }
                    showRestLeft = false;
                    showRestRight = false;
                }
                preferLeft = true;
            } else if (!hasLeftButtons && hasRightButtons) {
                if (isLeverReleased) {
                    const swingImg = images.get('swing_' + leverKey);
                    if (swingImg) {
                        swingImg.classList.remove('hidden');
                        swingImg.classList.add('visible');
                    }
                    showRestLeft = true;
                    showRestRight = false;
                } else {
                    const rImg = images.get('r_lever_' + leverKey);
                    if (rImg) {
                        rImg.classList.remove('hidden');
                        rImg.classList.add('visible');
                    }
                    showRestLeft = false;
                    showRestRight = false;
                }
                preferLeft = false;
            } else {
                if (isLeverReleased) {
                    const swingImg = images.get('swing_' + leverKey);
                    if (swingImg) {
                        swingImg.classList.remove('hidden');
                        swingImg.classList.add('visible');
                    }
                    showRestLeft = true;
                    showRestRight = true;
                } else {
                    if (preferLeft) {
                        const lImg = images.get('l_lever_' + leverKey);
                        if (lImg) {
                            lImg.classList.remove('hidden');
                            lImg.classList.add('visible');
                        }
                        showRestLeft = true;
                        showRestRight = false;
                    } else {
                        const rImg = images.get('r_lever_' + leverKey);
                        if (rImg) {
                            rImg.classList.remove('hidden');
                            rImg.classList.add('visible');
                        }
                        showRestLeft = false;
                        showRestRight = true;
                    }
                }
            }
            if (showRestLeft && restLeft) {
                restLeft.classList.remove('hidden');
                restLeft.classList.add('visible');
            }
            if (showRestRight && restRight) {
                restRight.classList.remove('hidden');
                restRight.classList.add('visible');
            }
            
            document.body.offsetHeight;
        }
        
        function handleButtonPress(btnName, imgKey, isLeft) {
            const motionKey = imgKey.replace('_on', '_motion');
            const motionImg = images.get(motionKey);
            
            if (isLeft) {
                leftPressedButtons.push(btnName);
                if (lastLeftMotion && lastLeftMotion !== motionKey) {
                    const oldMotion = images.get(lastLeftMotion);
                    if (oldMotion) {
                        oldMotion.classList.remove('visible');
                        oldMotion.classList.add('hidden');
                    }
                }
                
                if (motionImg) {
                    motionImg.classList.remove('hidden');
                    motionImg.classList.add('visible');
                    lastLeftMotion = motionKey;
                }
            } else {
                rightPressedButtons.push(btnName);
                if (lastRightMotion && lastRightMotion !== motionKey) {
                    const oldMotion = images.get(lastRightMotion);
                    if (oldMotion) {
                        oldMotion.classList.remove('visible');
                        oldMotion.classList.add('hidden');
                    }
                }
                
                if (motionImg) {
                    motionImg.classList.remove('hidden');
                    motionImg.classList.add('visible');
                    lastRightMotion = motionKey;
                }
            }
        }
        
        function handleButtonRelease(btnName, imgKey, isLeft) {
            const motionKey = imgKey.replace('_on', '_motion');
            const motionImg = images.get(motionKey);
            
            if (isLeft) {
                const index = leftPressedButtons.indexOf(btnName);
                if (index > -1) {
                    leftPressedButtons.splice(index, 1);
                }
                if (motionImg && lastLeftMotion === motionKey) {
                    motionImg.classList.remove('visible');
                    motionImg.classList.add('hidden');
                    lastLeftMotion = null;
                }
                if (leftPressedButtons.length > 0) {
                    const lastBtn = leftPressedButtons[leftPressedButtons.length - 1];
                    const lastImgKey = buttonMapping[lastBtn];
                    const lastMotionKey = lastImgKey.replace('_on', '_motion');
                    const lastMotionImg = images.get(lastMotionKey);
                    if (lastMotionImg) {
                        lastMotionImg.classList.remove('hidden');
                        lastMotionImg.classList.add('visible');
                        lastLeftMotion = lastMotionKey;
                    }
                }
            } else {
                const index = rightPressedButtons.indexOf(btnName);
                if (index > -1) {
                    rightPressedButtons.splice(index, 1);
                }
                if (motionImg && lastRightMotion === motionKey) {
                    motionImg.classList.remove('visible');
                    motionImg.classList.add('hidden');
                    lastRightMotion = null;
                }
                if (rightPressedButtons.length > 0) {
                    const lastBtn = rightPressedButtons[rightPressedButtons.length - 1];
                    const lastImgKey = buttonMapping[lastBtn];
                    const lastMotionKey = lastImgKey.replace('_on', '_motion');
                    const lastMotionImg = images.get(lastMotionKey);
                    if (lastMotionImg) {
                        lastMotionImg.classList.remove('hidden');
                        lastMotionImg.classList.add('visible');
                        lastRightMotion = lastMotionKey;
                    }
                }
            }
        }
        
        function pollState() {
            fetch('/state')
                .then(r => r.json())
                .then(data => updateDisplay(data))
                .catch(err => console.error('Poll error:', err));
        }
        
        setInterval(pollState, 16);
        pollState();
    </script>
</body>
</html>";
        }
    }

    public class InputState
    {
        public Dictionary<string, bool> buttons = new Dictionary<string, bool>();
        public LeverState lever = new LeverState();

        public InputState()
        {
            var buttonNames = new[] { 
                "Test", "Service", "Left1", "Left2", "Left3", 
                "Right1", "Right2", "Right3", "LeftWall", "RightWall",
                "LeftMenu", "RightMenu"
            };
            foreach (var name in buttonNames)
            {
                buttons[name] = false;
            }
        }

        public void UpdateFromJvs(Jvs jvs)
        {
            buttons["Test"] = jvs.getRawState(JvsButtonID.Test);
            buttons["Service"] = jvs.getRawState(JvsButtonID.Service);
            buttons["Left1"] = jvs.getRawState(JvsButtonID.Left1);
            buttons["Left2"] = jvs.getRawState(JvsButtonID.Left2);
            buttons["Left3"] = jvs.getRawState(JvsButtonID.Left3);
            buttons["Right1"] = jvs.getRawState(JvsButtonID.Right1);
            buttons["Right2"] = jvs.getRawState(JvsButtonID.Right2);
            buttons["Right3"] = jvs.getRawState(JvsButtonID.Right3);
            buttons["LeftWall"] = jvs.getRawState(JvsButtonID.LeftWall);
            buttons["RightWall"] = jvs.getRawState(JvsButtonID.RightWall);
            buttons["LeftMenu"] = jvs.getRawState(JvsButtonID.LeftMenu);
            buttons["RightMenu"] = jvs.getRawState(JvsButtonID.RightMenu);

            lever.value = jvs.getAnalog();
            lever.raw = jvs.getAnalogRaw();
            lever.direct = jvs.getAnalogDirect();
            lever.min = jvs.getAnalogMin();
            lever.max = jvs.getAnalogMax();
        }

        public void CopyFrom(InputState other)
        {
            foreach (var key in other.buttons.Keys)
            {
                buttons[key] = other.buttons[key];
            }
            lever.CopyFrom(other.lever);
        }

        public bool Equals(InputState other)
        {
            if (other == null) return false;
            
            foreach (var key in buttons.Keys)
            {
                if (buttons[key] != other.buttons[key])
                    return false;
            }
            
            return lever.Equals(other.lever);
        }

        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append("{");
            
            sb.Append("\"buttons\":{");
            bool first = true;
            foreach (var kvp in buttons)
            {
                if (!first) sb.Append(",");
                sb.Append($"\"{kvp.Key}\":{(kvp.Value ? "true" : "false")}");
                first = false;
            }
            sb.Append("},");
            
            sb.Append("\"lever\":{");
            sb.Append($"\"value\":{lever.value:F3},");
            sb.Append($"\"raw\":{lever.raw:F3},");
            sb.Append($"\"direct\":{lever.direct},");
            sb.Append($"\"min\":{lever.min:F3},");
            sb.Append($"\"max\":{lever.max:F3}");
            sb.Append("}");
            
            sb.Append("}");
            return sb.ToString();
        }
    }

    public class LeverState
    {
        public float value;
        public float raw;
        public long direct;
        public float min;
        public float max;

        public void CopyFrom(LeverState other)
        {
            value = other.value;
            raw = other.raw;
            direct = other.direct;
            min = other.min;
            max = other.max;
        }

        public bool Equals(LeverState other)
        {
            if (other == null) return false;
            return value == other.value && 
                   raw == other.raw && 
                   direct == other.direct &&
                   min == other.min &&
                   max == other.max;
        }
    }
}
