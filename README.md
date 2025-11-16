# OngekiPlugin_ButtonPrinterWeb

一个用于 ONGEKI 的 BepInEx 插件，通过网页界面实时显示按键和摇杆输入状态。

## 作者

- [@XiaHeng2333](https://github.com/XiaHeng2333)
- [@ds-xiaoyi](https://github.com/ds-xiaoyi)

## 功能特性

- 基于BepInEx,游戏内置按键捕捉，故兼容所有手台/模式
- 内置 HTTP 服务器，使用 System.Net.HttpListener

### 准备音击小女孩贴图

在插件目录中创建以下文件夹结构：
```
Package\BepInEx\plugins\images\buttons\
```

注:因贴图不是我们制作，图片请从[原作者项目](https://github.com/feziokabelia/OngekiButtonPrinterWeb)下载

## 使用方法

### 插件安装

将插件复制到游戏的 `BepInEx\plugins\` 目录

### 使用

1. 启动 SDDT
2. 插件会自动启动 HTTP 服务器，地址为 `http://127.0.0.1:9716/`

### 直播源设置

1. 在 OBS 中添加"浏览器"源
2. 设置 URL 为：`http://127.0.0.1:9716/`
3. 设置尺寸：宽度 600，高度 800

## 致谢

本插件灵感来源于 [OngekiButtonPrinterWeb](https://github.com/feziokabelia/OngekiButtonPrinterWeb)

感谢 [feziokabelia](https://github.com/feziokabelia)
