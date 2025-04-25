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