# PCL 2.99 下载功能恢复补丁

> **非官方项目**：与龙腾猫跃、PCL 官方团队、PCL 社区（PCL-Community）没有任何隶属、授权或合作关系。
> 官方版本请使用 [PCL-Community/PCL-CE](https://github.com/PCL-Community/PCL-CE) 或 [Meloong-Git/PCL](https://github.com/Meloong-Git/PCL)。
> 本仓库只恢复被移除的功能，不分发 PCL 本体，也不分发游戏文件。

PCL CE 2.99 把 Minecraft 下载相关代码在**编译期**删掉了：源码里的 `RestrictMcDownload` 是 `Const`，
编译器会把“正常分支”判成不可达代码直接丢弃，所以改 `PCL.ini`、注册表、甚至把二进制里的 `True` 改成 `False` 都没有用。

本仓库提供两种恢复方式：

| 方式 | 做法 | 恢复范围 |
| --- | --- | --- |
| **字节补丁**（推荐给普通用户） | 就地修改你已安装的官方 `Plain Craft Launcher 2.exe`，共 50 字节 | 下载页、实例安装/修复/补全 |
| **源码补丁**（需要 VS2022） | 把 `RestrictMcDownload` 改成 `False` 后重新编译 | 全部恢复，**包括整合包安装** |

---

## 一、字节补丁

### 适用版本

只支持下面这一个官方构建，其它版本（2.14、二次修改版、其他 2.99 构建）会被哈希校验拒绝：

| 项目 | 值 |
| --- | --- |
| 版本 | PCL CE 2.99.0.0 官方单文件 x64 |
| 大小 | 5,980,160 字节 |
| 原始 SHA-256 | `6B6E1260CB5A1D6A303855F1C60CAB3D0BC1AEBBA7E40A964180188B271E7386` |
| 修补后 SHA-256 | `98B494DD68B953988F445BFC29485C7D2014D472D8A7A685488A136FEA4D3F5F` |

### 使用

1. 完全退出 PCL，包括右下角托盘图标。
2. 运行 `PCL299-Download-Restore-Patch.exe`（也可以直接从 [Releases](https://github.com/baiji559/PCL299-Download-Restore-Patch/releases/latest) 下载已编译好的版本）。它会自动从桌面/开始菜单快捷方式、常见安装目录里找你已安装的 PCL；找不到就点“浏览”选中你的 `Plain Craft Launcher 2.exe`。
3. 点“开始修补”。
4. 看到“修补成功”后，照旧用原来的快捷方式启动 PCL，进入“下载”页。

补丁**不会产生新的启动器**，也不改配置和存档路径。第一次运行会在旁边生成 `Plain Craft Launcher 2.exe.pcl299.bak`；
想还原就再打开补丁点“还原备份”，或手动把 `.pcl299.bak` 覆盖回去。

如果 PCL 安装在 `Program Files` 等受保护目录，补丁会提示没有权限，点“是”可以自动以管理员身份重开。

### 原理

只改两处 IL 字节（偏移基于上述构建）：

| 偏移 | 长度 | 修改 |
| --- | --- | --- |
| `0x2C0D9` | 49 字节 | 把 `PageDownloadInstall.LoaderInit` 里构造 `LoaderTask("关闭下载", ...)` 的代码换成 `ldsfld ModDownload.DlClientListLoader`（其余用 `nop` 填充） |
| `0x322E6` | 1 字节 | `ldc.i4.2` → `ldc.i4.0`，让 `PageDownloadModify.LoadInstanceFinished` 里的 `CardMinecraft` 由 `Collapsed` 变回 `Visible` |

字节序列：

```
0x2C0D9:  14 7E 0F 04 00 04 00 ×43
0x322E6:  18 -> 16
```

没有伪造地区、没有改 hosts、没有挂代理，只是把编译器留下的“错误分支”替换回正常加载器。

### 自己编译补丁工具

只需要 Windows 自带的 .NET Framework 编译器（不需要 Visual Studio）：

```powershell
.\build.ps1
# 输出 bin\PCL299-Download-Restore-Patch.exe
```

命令行参数（便于脚本/批量使用）：

```text
PCL299-Download-Restore-Patch.exe --silent  "D:\PCL\Plain Craft Launcher 2.exe"
PCL299-Download-Restore-Patch.exe --restore "D:\PCL\Plain Craft Launcher 2.exe"
```

`--silent` 的结果写到目标旁边的 `.patch.log`。

---

## 二、源码补丁（完整恢复）

2.99 的源码里 `ModpackInstall` 的实现是完整的，只是编译时被 `Const True` 裁掉。
把常量改成 `False` 重新编译，所有分支都会回来：

```vb
' Plain Craft Launcher 2/Modules/ModSecret.vb
Public Const RestrictMcDownload As Boolean = False
```

完整 diff 见 [`source-patch/RestrictMcDownload.patch`](source-patch/RestrictMcDownload.patch)，其中还包括给自编译版本补一个公开的 Microsoft OAuth Client ID：

```vb
Public OAuthClientId As String = If(Environment.GetEnvironmentVariable("PCL_MS_CLIENT_ID"), "c14b0370-8d75-42f8-b329-5b60d39e319f")
```

### 编译步骤

环境要求：

- Windows 10/11
- Visual Studio 2022（或 Build Tools），勾选 **.NET 桌面开发** 与 **.NET Framework 4.8 目标包**
- Git
- PCL 2.99 源码（本仓库不包含）

```powershell
.\source-patch\build.ps1 -Source "D:\PCL-2.99.0.0"
```

脚本会自动改常量、克隆 [MeloongCore](https://github.com/Meloong-Git/MeloongCore)、找 MSBuild 并编译，产物在 `source-patch\out\Plain Craft Launcher 2.exe`。

编译后恢复的代码路径：

- 下载页：原版游戏、Forge / NeoForge / Fabric / Quilt
- 实例安装、修复、重置
- 启动前补全缺失文件
- 本地整合包安装、在线整合包安装
- 资源详情页的整合包安装卡片

> 这是“开源版本”构建，启动时会提示 CurseForge API、正版登录、更新与联网通知、主题、百宝箱受限；没有自动更新，不会被官方版覆盖。

---

## 已知限制

- 字节补丁只能补回“下载页/安装/修复/补全”，**整合包安装**在 2.99 里连实现代码都被编译删除了，无法用字节补丁恢复；需要整合包安装请用源码补丁重新编译。
- 启动器自带更新会覆盖字节补丁，更新后需要重新打一次。
- 补丁不修改游戏文件，也不绕过正版验证；请遵守当地法律与 Minecraft EULA。
- 所有产物都未签名，Windows 可能提示“未知发布者”。

## 仓库结构

```
src/PCL299Patch.cs               字节补丁工具源码（WinForms，.NET Framework 4.x）
build.ps1                        编译补丁工具
source-patch/RestrictMcDownload.patch   源码补丁（常量 + 内置 Client ID）
source-patch/build.ps1           从源码编译完整恢复版
LICENSE                          MIT（仅本仓库代码；PCL 本体版权归原作者）
```

## 许可

本仓库代码使用 [MIT License](LICENSE)。PCL 本体不包含在本仓库中，其版权归龙腾猫跃所有，使用时请遵守 PCL 自带许可证。

