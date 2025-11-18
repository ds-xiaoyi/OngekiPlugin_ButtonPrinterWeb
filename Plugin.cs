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
using WebSocketSharp.Server;
using WebSocketSharp;

namespace InputMonitorMod
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger = null!;

        static PluginConfig config = null!;
        static string listenAddr = null!;
        
        static InputState currentState = null!;
        static InputState exportedState = null!;
        
        private HttpServer server = null!;
        private int frameCounter = 0;

        private void Awake()
        {
            Logger = base.Logger;
            Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        }

        private void Start()
        {
            Harmony.CreateAndPatchAll(typeof(Plugin));
            Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} Started!");

            config = PluginConfig.Load(Logger);
            listenAddr = $"http://127.0.0.1:{config.Port}/";

            currentState = new InputState(config);
            exportedState = new InputState(config);

            StartWebSocketServer();
        }

        private void OnDestroy()
        {
            StopWebSocketServer();
        }

        private void Update()
        {
            frameCounter++;
            if (frameCounter % config.FrameSkip != 0) return;
            
            Jvs jvs = MechaManager.jvs;
            if (jvs == null) return;

            currentState.UpdateFromJvs(jvs);

            if (!currentState.isDirty) return;

            if (currentState.HasChanges(exportedState))
            {
                lock (exportedState)
                {
                    exportedState.CopyFrom(currentState);
                    currentState.isDirty = false;
                    Monitor.Pulse(exportedState);
                }
            }
            else
            {
                currentState.isDirty = false;
            }
        }

        private void StartWebSocketServer()
        {
            try
            {
                server = new HttpServer(listenAddr);
                server.OnGet += (sender, e) =>
                {
                    var req = e.Request;
                    var res = e.Response;
                    if (req.RawUrl == "/")
                    {
                        res.ContentType = "text/html";
                        byte[] buffer = Encoding.UTF8.GetBytes(GetHtmlPage());
                        res.ContentLength64 = buffer.Length;
                        res.OutputStream.Write(buffer, 0, buffer.Length);
                        res.Close();
                    }
                    else if (req.RawUrl.StartsWith("/images/"))
                    {
                        ServeStaticFile(req, res);
                    }
                    else
                    {
                        res.StatusCode = 404;
                        res.Close();
                    }
                };
                server.AddWebSocketService<InputStateService>("/state", () =>
                {
                    return new InputStateService(exportedState, Logger);
                });
                server.Start();
                Logger.LogInfo($"WebSocket Server started on {listenAddr}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to start WebSocket server: {ex.Message}");
            }
        }

        private void StopWebSocketServer()
        {
            if (server != null && server.IsListening)
            {
                server.Stop();
            }
        }

        private void ServeStaticFile(WebSocketSharp.Net.HttpListenerRequest request, WebSocketSharp.Net.HttpListenerResponse response)
        {
            try
            {
                string pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string relativePath = request.RawUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
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
                response.Close();
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
            display: flex;
            justify-content: center;
            align-items: center;
            min-height: 100vh;
            overflow: hidden;
        }
        canvas {
            width: 600px;
            height: 800px;
            image-rendering: -webkit-optimize-contrast;
            image-rendering: crisp-edges;
        }
    </style>
</head>
<body>
    <canvas id='canvas'></canvas>
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
        
        const canvas = document.getElementById('canvas');
        const ctx = canvas.getContext('2d');
        const dpr = window.devicePixelRatio || 1;
        canvas.width = 600 * dpr;
        canvas.height = 800 * dpr;
        ctx.scale(dpr, dpr);
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
        const leftButtons = new Set(['LeftWall', 'Left1', 'Left2', 'Left3']);
        const rightButtons = new Set(['Right1', 'Right2', 'Right3', 'RightWall']);
        let loadedCount = 0;
        BUTTONS_DATA.forEach(button => {
            const img = new Image();
            img.onload = () => { loadedCount++; };
            img.src = button.image_url;
            images.set(button.key, img);
        });
        const waitingImg = new Image();
        waitingImg.src = 'images/buttons/waiting.png';
        images.set('waiting', waitingImg);
        
        let lastLeverKey = '0';
        let lastLeverPos = 0;
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
        let currentRenderState = {
            buttons: {},
            leverKey: '0',
            showRestLeft: true,
            showRestRight: false,
            leftMotion: null,
            rightMotion: null
        };
        
        function updateDisplay(data) {
            if (firstUpdate) {
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
                } else if (!isPressed && wasPressed) {
                    handleButtonRelease(btnName, imgKey, isLeft);
                }
                currentRenderState.buttons[imgKey] = isPressed;
                previousButtonStates[btnName] = isPressed;
            }
            const hasLeftButtons = leftPressedButtons.length > 0;
            const hasRightButtons = rightPressedButtons.length > 0;
            const leverKey = getLeverKey(data.lever.value);
            const currentLeverPos = data.lever.raw;
            const isLeverReleased = data.lever.isReleased || false;
            if (currentLeverPos !== lastLeverPos) {
                lastLeverKey = leverKey;
                lastLeverPos = currentLeverPos;
            }
            currentRenderState.leverKey = leverKey;
            currentRenderState.showRestLeft = false;
            currentRenderState.showRestRight = false;
            if (hasLeftButtons && hasRightButtons) {
                currentRenderState.leverType = 'swing';
            } else if (hasLeftButtons && !hasRightButtons) {
                if (isLeverReleased) {
                    currentRenderState.leverType = 'swing';
                    currentRenderState.showRestRight = true;
                } else {
                    currentRenderState.leverType = 'l_lever';
                }
                preferLeft = true;
            } else if (!hasLeftButtons && hasRightButtons) {
                if (isLeverReleased) {
                    currentRenderState.leverType = 'swing';
                    currentRenderState.showRestLeft = true;
                } else {
                    currentRenderState.leverType = 'r_lever';
                }
                preferLeft = false;
            } else {
                if (isLeverReleased) {
                    currentRenderState.leverType = 'swing';
                    currentRenderState.showRestLeft = true;
                    currentRenderState.showRestRight = true;
                } else {
                    currentRenderState.leverType = preferLeft ? 'l_lever' : 'r_lever';
                    if (preferLeft) {
                        currentRenderState.showRestLeft = true;
                    } else {
                        currentRenderState.showRestRight = true;
                    }
                }
            }
            currentRenderState.leftMotion = lastLeftMotion;
            currentRenderState.rightMotion = lastRightMotion;
        }
        
        function handleButtonPress(btnName, imgKey, isLeft) {
            const motionKey = imgKey.replace('_on', '_motion');
            if (isLeft) {
                leftPressedButtons.push(btnName);
                lastLeftMotion = motionKey;
            } else {
                rightPressedButtons.push(btnName);
                lastRightMotion = motionKey;
            }
        }
        
        function handleButtonRelease(btnName, imgKey, isLeft) {
            const motionKey = imgKey.replace('_on', '_motion');
            if (isLeft) {
                const index = leftPressedButtons.indexOf(btnName);
                if (index > -1) leftPressedButtons.splice(index, 1);
                if (lastLeftMotion === motionKey) lastLeftMotion = null;
                if (leftPressedButtons.length > 0) {
                    const lastBtn = leftPressedButtons[leftPressedButtons.length - 1];
                    lastLeftMotion = buttonMapping[lastBtn].replace('_on', '_motion');
                }
            } else {
                const index = rightPressedButtons.indexOf(btnName);
                if (index > -1) rightPressedButtons.splice(index, 1);
                if (lastRightMotion === motionKey) lastRightMotion = null;
                if (rightPressedButtons.length > 0) {
                    const lastBtn = rightPressedButtons[rightPressedButtons.length - 1];
                    lastRightMotion = buttonMapping[lastBtn].replace('_on', '_motion');
                }
            }
        }
        
        const ws = new WebSocket('ws://127.0.0.1:" + config.Port + @"/state');
        ws.binaryType = 'arraybuffer';
        
        ws.onopen = function() {
            console.log('[WebSocket] Connected');
            ws.send('request_state');
        };
        
        ws.onmessage = function(event) {
            try {
                if (event.data instanceof ArrayBuffer) {
                    const data = parseBinaryData(event.data);
                    updateDisplay(data);
                    render();
                } else {
                    console.error('[WebSocket] Unexpected data type:', typeof event.data);
                }
            } catch (err) {
                console.error('[WebSocket] Parse error:', err);
            }
        };
        
        function drawImg(img) {
            if (!img || !img.complete) return;
            const scale = Math.min(600 / img.width, 800 / img.height);
            const w = img.width * scale;
            const h = img.height * scale;
            const x = (600 - w) / 2;
            const y = (800 - h) / 2;
            ctx.drawImage(img, x, y, w, h);
        }
        function render() {
            ctx.clearRect(0, 0, 600, 800);
            drawImg(images.get('waiting'));
            if (currentRenderState.showRestLeft) drawImg(images.get('rest_l'));
            if (currentRenderState.showRestRight) drawImg(images.get('rest_r'));
            for (const [key, pressed] of Object.entries(currentRenderState.buttons)) {
                if (pressed) drawImg(images.get(key));
            }
            if (currentRenderState.leftMotion) drawImg(images.get(currentRenderState.leftMotion));
            if (currentRenderState.rightMotion) drawImg(images.get(currentRenderState.rightMotion));
            if (currentRenderState.leverType) {
                drawImg(images.get(currentRenderState.leverType + '_' + currentRenderState.leverKey));
            }
        }
        
        function parseBinaryData(buffer) {
            const view = new DataView(buffer);
            
            const buttonBits = view.getUint16(0, true);
            
            const leverRaw = view.getFloat32(2, true);
            
            const leverDirect = view.getInt32(6, true);
            
            const isReleased = view.getUint8(10) !== 0;
            
            const buttonNames = [
                'Test', 'Service', 'Left1', 'Left2', 'Left3',
                'Right1', 'Right2', 'Right3', 'LeftWall', 'RightWall',
                'LeftMenu', 'RightMenu'
            ];
            
            const buttons = {};
            for (let i = 0; i < buttonNames.length; i++) {
                buttons[buttonNames[i]] = (buttonBits & (1 << i)) !== 0;
            }
            
            return {
                buttons: buttons,
                lever: {
                    raw: leverRaw,
                    direct: leverDirect,
                    isReleased: isReleased,
                    value: leverRaw
                }
            };
        }
        
        ws.onerror = function(error) {
            console.error('[WebSocket] Error:', error);
        };
        
        ws.onclose = function() {
            console.log('[WebSocket] Disconnected, reconnecting...');
            setTimeout(function() {
                location.reload();
            }, 1000);
        };
    </script>
</body>
</html>";
        }
    }

    public class InputState
    {
        private static readonly string[] ButtonNames = new[] { 
            "Test", "Service", "Left1", "Left2", "Left3", 
            "Right1", "Right2", "Right3", "LeftWall", "RightWall",
            "LeftMenu", "RightMenu"
        };
        
        public Dictionary<string, bool> buttons = new Dictionary<string, bool>();
        public LeverState lever = new LeverState();
        
        private float lastLeverRaw = 0f;
        private long lastLeverDirect = 0;
        private float leverStableTime = 0f;
        private DateTime lastUpdateTime = DateTime.Now;
        
        private PluginConfig config;
        
        public bool isDirty = false;

        public InputState(PluginConfig config)
        {
            this.config = config;
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
            bool changed = false;
            
            bool testState = jvs.getRawState(JvsButtonID.Test);
            if (buttons["Test"] != testState) { buttons["Test"] = testState; changed = true; }
            
            bool serviceState = jvs.getRawState(JvsButtonID.Service);
            if (buttons["Service"] != serviceState) { buttons["Service"] = serviceState; changed = true; }
            
            bool left1State = jvs.getRawState(JvsButtonID.Left1);
            if (buttons["Left1"] != left1State) { buttons["Left1"] = left1State; changed = true; }
            
            bool left2State = jvs.getRawState(JvsButtonID.Left2);
            if (buttons["Left2"] != left2State) { buttons["Left2"] = left2State; changed = true; }
            
            bool left3State = jvs.getRawState(JvsButtonID.Left3);
            if (buttons["Left3"] != left3State) { buttons["Left3"] = left3State; changed = true; }
            
            bool right1State = jvs.getRawState(JvsButtonID.Right1);
            if (buttons["Right1"] != right1State) { buttons["Right1"] = right1State; changed = true; }
            
            bool right2State = jvs.getRawState(JvsButtonID.Right2);
            if (buttons["Right2"] != right2State) { buttons["Right2"] = right2State; changed = true; }
            
            bool right3State = jvs.getRawState(JvsButtonID.Right3);
            if (buttons["Right3"] != right3State) { buttons["Right3"] = right3State; changed = true; }
            
            bool leftWallState = jvs.getRawState(JvsButtonID.LeftWall);
            if (buttons["LeftWall"] != leftWallState) { buttons["LeftWall"] = leftWallState; changed = true; }
            
            bool rightWallState = jvs.getRawState(JvsButtonID.RightWall);
            if (buttons["RightWall"] != rightWallState) { buttons["RightWall"] = rightWallState; changed = true; }
            
            bool leftMenuState = jvs.getRawState(JvsButtonID.LeftMenu);
            if (buttons["LeftMenu"] != leftMenuState) { buttons["LeftMenu"] = leftMenuState; changed = true; }
            
            bool rightMenuState = jvs.getRawState(JvsButtonID.RightMenu);
            if (buttons["RightMenu"] != rightMenuState) { buttons["RightMenu"] = rightMenuState; changed = true; }

            DateTime now = DateTime.Now;
            float deltaTime = (float)(now - lastUpdateTime).TotalSeconds;
            lastUpdateTime = now;

            float currentRaw = jvs.getAnalogRaw();
            long currentDirect = jvs.getAnalogDirect();
            
            if (currentRaw != lastLeverRaw)
            {
                leverStableTime = 0f;
                lastLeverRaw = currentRaw;
                changed = true;
            }
            else if (currentDirect != lastLeverDirect)
            {
                leverStableTime = 0f;
                changed = true;
            }
            else
            {
                leverStableTime += deltaTime;
                if (leverStableTime >= config.LeverReleaseTime)
                {
                    changed = true;
                }
            }
            lastLeverDirect = currentDirect;
            
            lever.value = jvs.getAnalog();
            lever.raw = currentRaw;
            lever.direct = currentDirect;
            lever.min = jvs.getAnalogMin();
            lever.max = jvs.getAnalogMax();
            lever.isReleased = leverStableTime > config.LeverReleaseTime;
            
            if (changed)
            {
                isDirty = true;
            }
        }

        public void CopyFrom(InputState other)
        {
            foreach (var key in other.buttons.Keys)
            {
                buttons[key] = other.buttons[key];
            }
            lever.CopyFrom(other.lever);
            lastLeverRaw = other.lastLeverRaw;
            lastLeverDirect = other.lastLeverDirect;
            leverStableTime = other.leverStableTime;
            lastUpdateTime = other.lastUpdateTime;
        }

        public bool HasChanges(InputState other)
        {
            if (other == null) return true;
            
            if (!lever.Equals(other.lever))
                return true;
            
            foreach (var key in buttons.Keys)
            {
                if (buttons[key] != other.buttons[key])
                    return true;
            }
            
            return false;
        }
        
        public bool Equals(InputState other)
        {
            return !HasChanges(other);
        }
        
        public byte[] ToBinary()
        {
            byte[] data = new byte[11];
            
            ushort buttonBits = 0;
            for (int i = 0; i < ButtonNames.Length && i < 12; i++)
            {
                if (buttons.ContainsKey(ButtonNames[i]) && buttons[ButtonNames[i]])
                {
                    buttonBits |= (ushort)(1 << i);
                }
            }
            data[0] = (byte)(buttonBits & 0xFF);
            data[1] = (byte)((buttonBits >> 8) & 0xFF);
            
            byte[] rawBytes = BitConverter.GetBytes(lever.raw);
            Buffer.BlockCopy(rawBytes, 0, data, 2, 4);
            
            byte[] directBytes = BitConverter.GetBytes((int)lever.direct);
            Buffer.BlockCopy(directBytes, 0, data, 6, 4);
            
            data[10] = (byte)(lever.isReleased ? 1 : 0);
            
            return data;
        }
    }

    public class LeverState
    {
        public float value;
        public float raw;
        public long direct;
        public float min;
        public float max;
        public bool isReleased;

        public void CopyFrom(LeverState other)
        {
            value = other.value;
            raw = other.raw;
            direct = other.direct;
            min = other.min;
            max = other.max;
            isReleased = other.isReleased;
        }

        public bool Equals(LeverState other)
        {
            if (other == null) return false;
            return value == other.value && 
                   raw == other.raw && 
                   direct == other.direct &&
                   min == other.min &&
                   max == other.max &&
                   isReleased == other.isReleased;
        }
    }

    public class InputStateService : WebSocketBehavior
    {
        private InputState state;
        private ManualLogSource logger;
        private Thread watcher;

        public InputStateService(InputState state, ManualLogSource logger)
        {
            this.state = state;
            this.logger = logger;
            this.watcher = new Thread(() => { this.ServiceLoop(); });
            watcher.IsBackground = true;
            watcher.Start();
        }

        private void ServiceLoop()
        {
            while (true)
            {
                byte[] binaryData;
                lock (state)
                {
                    Monitor.Wait(state);
                    binaryData = state.ToBinary();
                }
                Sessions.Broadcast(binaryData);
            }
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            if (e.Data == "request_state")
            {
                byte[] binaryData;
                lock (state)
                {
                    binaryData = state.ToBinary();
                }
                Send(binaryData);
            }
        }
    }
}
