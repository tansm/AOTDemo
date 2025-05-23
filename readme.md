# 使用 AOT 编译保护 .NET 核心逻辑，同时支持第三方扩展

## 引言

在开发大型ERP .NET 应用程序时，我面临一个挑战：如何创建一个可供第三方引用的组件（DLL）以便二次开发，但同时保护核心逻辑不被轻易反编译，还要支持反射机制（包括私有字段访问），并且坚持使用 C# 开发，而非 C++/CLI。在这篇博客中，我将分享我的探索历程，包括遇到的困难、尝试的方案，以及最终实现的解决方案。这个方案利用了 Ahead-of-Time（AOT）编译，成功实现了核心逻辑保护和第三方扩展的完美平衡。

## 场景与需求

我需要开发一个 .NET 组件（DLL），供第三方开发者引用，同时满足以下要求：

- **可作为 .NET 组件引用**：第三方应能通过项目引用或 NuGet 包直接使用我的组件。
- **保护核心逻辑**：核心逻辑不能被简单反编译为可读的 C# 代码。
- **支持反射**：包括对私有字段的反射访问，需保持完整功能。
- **使用 C# 开发**：避免使用 C++/CLI，坚持使用 C# 以保持开发一致性。

此外，我希望第三方开发者能够开发插件，扩展我的应用程序功能，同时在调试时只看到自己的代码，而不会暴露我的核心逻辑。

## 尝试的方案及其优缺点

在找到最终解决方案之前，我尝试了多种方法，每种方法都有其优点和局限性。

### 方案 1：直接使用 AOT 编译

我首先尝试使用 .NET 的 Native AOT 编译，将我的 DLL 编译为本地代码，以移除 IL（中间语言），从而增加反编译难度。然而，我发现：

- **优点**：
  - AOT 编译生成纯机器码，难以反编译。
  - 启动性能优异，适合高性能场景。
- **缺点**：
  - AOT默认编译出单一的exe；
  - 虽然后面成功让 AOT 编译生成的 DLL ，但他是本地库，无法作为标准 .NET 程序集被引用。

**结论**：AOT 编译不适合直接生成可引用的 .NET 组件。

### 方案 2：使用 PublishReadyToRun

接下来，我尝试了 `PublishReadyToRun` 选项，它将 IL 预编译为本地代码，同时保留 IL。我希望这能提高性能并增加反编译难度。然而：

- **优点**：
  - 生成的 DLL 是标准 .NET 程序集，可被第三方引用。
  - 启动性能有所提升。
- **缺点**：
  - IL 仍然存在，仍然可以被反编译工具（如 ILSpy 或 dotPeek）读取。
  - 无法完全保护核心逻辑。

**结论**：PublishReadyToRun 无法满足移除 IL 和防止反编译的要求。

### 方案 3：使用混淆工具

我还考虑了使用混淆工具（如 Eazfuscator.NET 或 .NET Reactor）来保护代码。这些工具通过重命名符号、加密字符串等方式使 IL 难以阅读。然而：

- **优点**：
  - 显著增加反编译难度。
  - 支持通过属性（如 `ObfuscationAttribute`）保留反射功能。
- **缺点**：
  - IL 仍然存在，理论上仍可被高级工具反编译。
  - 配置复杂，尤其是需要保留反射的私有字段时。
  - 仅使代码难以阅读，而非完全不可读。

**结论**：混淆工具虽有效，但无法完全消除 IL，且与反射需求存在潜在冲突。

## 最终解决方案

经过多次尝试，我设计了一个结合 AOT 编译和插件架构的解决方案，成功满足了所有需求。以下是方案的详细说明。

### 解决方案概述

我将组件分为以下四个部分：

1. **AOTDemo.Services.dll**：
   - 包含接口（`IDemoService`、`IPlugin`）和简单类（`QueryArgs`）。
   - 作为标准 .NET 程序集，供第三方直接引用。
   - 不包含核心逻辑，公开提供给第三方。

2. **AOTDemo.dll**：
   - 包含核心逻辑的实现（`DemoService`），实现 `IDemoService` 接口。
   - 通过 AOT 编译为本地代码，保护核心逻辑不被反编译。
   - 被主程序引用。

3. **MyApp.exe**：
   - 主程序，负责启动应用程序。
   - 通过 AOT 编译，包含 `AOTDemo.dll` 的核心逻辑。
   - 动态加载第三方插件，并通过服务容器提供核心服务。

4. **MyPlugin.dll**：
   - 第三方开发的插件，引用 `AOTDemo.Services.dll`。
   - 实现 `IPlugin` 接口，通过服务容器访问核心服务。

### 工作流程

- **开发阶段**：
  - 我在 `AOTDemo.Services.dll` 中定义接口和简单类。
  - 在 `AOTDemo.dll` 中实现核心逻辑。
  - `MyApp.exe` 引用 `AOTDemo.dll` 并通过 AOT 编译。
  - 第三方开发者引用 `AOTDemo.Services.dll`，开发插件（如 `MyPlugin.dll`）。

- **运行时**：
  - `MyApp.exe` 启动，创建服务容器并注册核心服务（`DemoService`）。
  - 动态加载第三方插件（`MyPlugin.dll`），通过反射创建插件实例。
  - 插件通过服务容器调用核心服务，执行功能。
  - 第三方调试时，堆栈仅显示插件代码，不包含 `MyApp.exe` 或 `AOTDemo.dll` 的内部方法。

### 项目结构与代码示例

以下是关键文件的内容和配置。

#### AOTDemo.Services.dll

- **Services.cs**：定义接口和数据类。
```csharp
namespace AOTDemo.Services
{
    public class QueryArgs
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
    }

    public interface IDemoService
    {
        string query(string query, QueryArgs args);
    }

    public interface IPlugin
    {
        int Run();
    }
}
```

#### AOTDemo.dll

- **ServiceImpl.cs**：实现核心逻辑。
```csharp
using AOTDemo.Services;
using System.Runtime.InteropServices;

namespace AOTDemo
{
    public class DemoService : IDemoService
    {
        public string query(string query, QueryArgs args)
        {
            return $"exe:{query}, Name: {args.Name}, Age: {args.Age}";
        }
    }
}
```

#### MyApp.exe

- **Program.cs**：主程序，加载插件并提供服务。
```csharp
// 创建一个服务容器
using AOTDemo;
using AOTDemo.Services;
using System.ComponentModel.Design;
using System.Reflection;

// 创建一个服务容器，将服务注册到容器中
var services = new ServiceContainer();
services.AddService(typeof(IDemoService), new DemoService());

// 并且 MyPlugin.dll 位于当前工作目录或指定路径
var pluginAssemblyPath = args[0]; // "MyPlugin.dll"; // 外部 DLL 的路径
var pluginAssembly = Assembly.LoadFrom(pluginAssemblyPath); // 加载 DLL

// 从参数的 1 参数，获取 一个 class 名称，模拟使用反射获取一个外部插件
var pluginName = args[1];
var pluginType = pluginAssembly.GetType(pluginName);
if (pluginType == null) {
    Console.WriteLine($"无法找到类型: {pluginName}");
    return;
}

// 创建插件实例
// 注意插件的构造函数第一个参数是一个 IServiceProvider
var plugin = (IPlugin?)Activator.CreateInstance(pluginType, services);
if (plugin == null) {
    Console.WriteLine($"无法创建插件实例: {pluginName}");
    return;
}

// 运行插件
plugin.Run();

// 演示即使加载的是外部 DLL，没有进行AOT编译，也可以使用反射
plugin.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
    .ToList()
    .ForEach(f => Console.WriteLine($"Field: {f.Name} , value = {f.GetValue(plugin)}"));
```

#### MyPlugin.dll

- **MyPlugin.cs**：第三方插件示例。
```csharp
using AOTDemo.Services;
using System.Reflection;

namespace AOTDemo {
    public class MyPlugin : IPlugin {
        private IServiceProvider _serviceProvider;
        public MyPlugin(IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
        }

        public int Run() {
            // 假装获取一个服务
            var service = (IDemoService)_serviceProvider.GetService(typeof(IDemoService));
            var args = new QueryArgs() { Name = "AOT", Age = 18 };
            var result = service.query("Hello", args);
            Console.WriteLine(result);

            // 演示即使外部服务进行AOT编译，也可以使用反射
            var queryMethod = service.GetType().GetMethod("query", BindingFlags.Public | BindingFlags.Instance);
            result = (String)queryMethod.Invoke(service, new object[] { "Hello2", args });
            Console.WriteLine(result);

            return 0;
        }
    }
}
```

### 发布与运行

- **发布 MyApp.exe**：
  - 使用Visual Studio 发布 AOT 编译的 `MyApp.exe`，我创建 win-x64,单个exe文件，你可以根据自己需要创建更多的类型；
  - 因为主项目引用了 `AOTDemo.dll`，所以自然一并AOT了。

- **分发 AOTDemo.Services.dll**：
  - 直接提供 `AOTDemo.Services.dll` 给第三方，他是标准 .NET 程序集，不经过AOT处理。

- **运行**：
  - 最终执行只需要主 exe 和 第三方插件；
  - 运行 `MyApp.exe`，指定插件 DLL 和类名：
    ```bash
    MyApp.exe MyPlugin.dll AOTDemo.MyPlugin
    ```
  - 插件通过服务容器调用核心服务，输出结果。

![alt text](Run.png)

### 调试体验

一个令人兴奋的成果是，第三方开发者在调试插件时，调用堆栈仅显示他们的代码（`MyPlugin.dll`），不包含 `MyApp.exe` 或 `AOTDemo.dll` 的内部方法。这得益于插件架构的隔离设计，确保核心逻辑对第三方完全透明。

![alt text](PluginDebug.png)

而且你可以看见，核心组件的字段和.net组件的调试信息仍然存在，没有降低开发体验；

## 方案优点与局限性

### 优点

- **核心逻辑保护**：`AOTDemo.dll` 和 `MyApp.exe` 通过 AOT 编译为本地代码，无 IL，难以反编译。
- **标准 .NET 引用**：`AOTDemo.Services.dll` 是标准 .NET 程序集，第三方可轻松引用。
- **反射支持**：`AOTDemo.Services.dll` 保留完整元数据，支持反射，包括私有字段。
- **扩展性**：插件架构允许第三方开发自定义功能。
- **C# 开发**：整个解决方案使用 C#，无需 C++/CLI。
- **调试隔离**：第三方调试时仅看到自己的代码，保护核心逻辑隐私。

### 局限性

- **AOT 编译限制**：运维和调试流程与常见的.net组件不是完全一致，需要一些学习。
- **分发复杂性**：需要为不同平台（如 Windows、Linux）提供 AOT 编译的 `MyApp.exe`。

## 对比分析

以下表格总结了不同方案的优缺点：

| 方案                | 可作为 .NET 组件引用 | 保护代码效果 | 反射支持 | 开发复杂性 | 备注 |
|--------------------|---------------------|-------------|---------|-----------|------|
| 直接 AOT 编译       | 否                  | 好（无 IL）  | 有限     | 中等       | 无法直接引用 |
| PublishReadyToRun   | 是                  | 一般（保留 IL） | 是       | 低         | 可反编译 |
| 混淆工具            | 是                  | 一般（IL 难以阅读） | 是（需配置） | 低         | IL 仍存在 |
| 本方案（AOT + 接口） | 是                  | 好（核心逻辑无 IL） | 是       | 低       | 推荐方案 |

## 结论

通过将组件分为接口定义（`AOTDemo.Services.dll`）、核心逻辑（`AOTDemo.dll`）和主程序（`MyApp.exe`），并结合 AOT 编译和插件架构，我成功实现了一个既保护核心逻辑又支持第三方扩展的 .NET 解决方案。这个方案不仅满足了我的所有需求，还提供了良好的调试体验，让第三方开发者能够专注于自己的代码，而无需接触核心逻辑。

我希望这个解决方案能为其他 .NET 开发者提供启发，特别是在需要保护知识产权和支持扩展性的场景中。感谢探索过程中的挑战，它们让我找到了这个令人满意的答案！

## 代码

我已经将演示代码放在开源社区，有兴趣的朋友可以下载尝试。
https://github.com/tansm/AOTDemo