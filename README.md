# OngekiPlugin_ButtonPrinterWeb

一个用于 ONGEKI 的 BepInEx 插件，通过网页界面实时显示按键和摇杆输入状态。

## 作者

- [@XiaHeng2333](https://github.com/XiaHeng2333)
- [@ds-xiaoyi](https://github.com/ds-xiaoyi)

## 版本

- v1.2.0 重构前端渲染：DOM -> Canvas

## 功能特性

- 基于BepInEx,游戏内置按键捕捉，故兼容所有手台/模式
- 基于WebSocket

### 准备音击小女孩贴图

在插件目录中创建以下文件夹结构：
```
Package\BepInEx\plugins\images\buttons\
```

注:因贴图不是我们制作，图片请从[原作者专栏](https://www.bilibili.com/opus/1091366492935553030)下载

## 使用方法

### 插件安装

将插件复制到游戏的 `BepInEx\plugins\` 目录

### 使用

1. 启动 SDDT
2. 插件会自动启动 HTTP 服务器，地址为 `http://127.0.0.1:8000/`
3. 插件在启动一次之后会自动在插件目录生成一个配置文件，详见下方

### 配置文件示例

```
# Config

[Network]
# WebSocket 服务器端口号 (默认: 8000)
# 如果端口被占用，可以修改为其他端口
Port = 8000

[Performance]
# 帧数跳过设置，每 N 帧检查一次状态 (默认: 2，即每2帧检查一次)
# 建议范围: 1-4（基于60hz刷新率计算，1等效于60fps，4等效于15fps）
FrameSkip = 2

[Lever]
# 摇杆松手判定时间（秒）(默认: 0.15)
LeverReleaseTime = 0.15
# 摇杆静止超过此时间后判定音击小孩为松手
```

### 直播源设置

1. 在 OBS 中添加"浏览器"源
2. 设置 URL 为：`http://127.0.0.1:8000/`
3. 设置尺寸：宽度 600，高度 800

## 致谢

本插件灵感来源于 [OngekiButtonPrinterWeb](https://github.com/feziokabelia/OngekiButtonPrinterWeb)

感谢 [feziokabelia](https://github.com/feziokabelia)
