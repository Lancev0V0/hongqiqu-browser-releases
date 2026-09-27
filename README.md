# 红旗渠爱国浏览器

Windows x64 的独立 Firefox 主题与启动器：象牙白留白、中国红、红旗渠山水，顶部标签栏和居中搜索。自带官方 Firefox 引擎、独立个人配置、任务栏图标和 GitHub 应用更新。

## 安装

从 [Releases](https://github.com/Lancev0V0/hongqiqu-browser-releases/releases/latest) 下载 `HongqiquBrowser-Setup-版本-win-x64.exe`，安装后使用桌面或开始菜单的“红旗渠爱国浏览器”。支持 Windows 10/11 x64 和 .NET Framework 4.8。

程序安装到 `%LOCALAPPDATA%\Programs\HongqiquBrowser`，数据保存在 `%LOCALAPPDATA%\HongqiquBrowser\profile`。更新和卸载保留个人数据。安装包不包含制作者的书签、密码、Cookie 或历史记录。Firefox 内核保持 Mozilla 签名，主题与窗口标识由启动器应用。首次启动的 Firefox 条款页面仍由 Firefox 管理，诊断数据默认为关闭。

## 更新规则

每次通过启动器新开浏览器时，检查本仓库最新正式 Release。未发现新版本时允许离线启动；检测到有效新版后，将要求持久保存在本机，只有更新成功才能继续启动。失败界面只有重试和退出。已经打开的浏览会话不会突然退出或丢失未保存内容。

更新包的仓库地址、版本、大小和 SHA-256 必须与 GitHub 发布信息一致；下载校验并完整解压成功后，原子切换 `current.txt`。旧版本和独立个人配置保留。发布者不得删除已公布的必需版本；修复问题应发布更高版本。普通用户启动路径执行强制更新，拥有本机文件控制权的人仍可直接运行底层引擎；本项目没有设备管理或防篡改策略。

Firefox 引擎的自动更新由整包发布接管，维护者必须及时跟进 Mozilla 安全版本。此项目不会自动生成安全修复或自动发布 Mozilla 新版。

## 构建和发布

1. 安装 7-Zip，使用 Windows PowerShell 7 / `pwsh` 和系统 .NET Framework 编译器。
2. 运行 `./test.ps1`，再运行 `./build.ps1`。产物在 `dist/`。
3. 更新 `build-config.json` 中的引擎版本与 Mozilla 官方 SHA-512，并同步第三方声明；自定义启动器版本使用 `X.Y.Z`。
4. 提交变更并推送 `vX.Y.Z` 标签。GitHub Actions 自动测试、构建、上传安装 EXE、完整更新 ZIP 和校验文件，上传完成才公开 Release。

本地构建可传入 `-FirefoxInstaller`、`-NsisPath`、`-SevenZipPath` 和 `-BuildRoot`。构建会同时验证 Mozilla 安装文件 SHA-512 和 Authenticode 签名。不得在已发布版本下替换文件；发布新版本。

## 1.0.1 验证记录（2026-09-27）

- Windows 实机安装成功，并完成从 1.0.0 到 GitHub 正式版本 1.0.1 的下载、校验、切换和重新启动。
- 运行窗口的 32 / 256 像素图标与自定义图标完全一致；桌面快捷方式和窗口使用同一个独立 AppUserModelID，任务栏再次启动入口指向更新启动器。
- 升级前后的书签记录一致；发行 ZIP 检查未包含书签库、Cookie、登录凭据或制作者的个人配置。
- 28 项更新规则与异常恢复测试通过；使用正式更新代码访问公开 GitHub 源、下载、SHA-256 校验和解压通过。
- [GitHub Windows 云端构建通过](https://github.com/Lancev0V0/hongqiqu-browser-releases/actions/runs/36311504471)。手工触发的此轮检查覆盖构建与产物上传，标签发布步骤未在此轮执行；1.0.1 正式 Release 已单独上传并核对远端摘要。

## 签名与来源

启动器和安装程序当前未购买代码签名证书，Windows 可能提示未知发布者。Firefox 内核为未经修改的 Mozilla 官方签名文件。本项目不是 Mozilla 或任何政府机构的官方产品。组件来源与许可证见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。
