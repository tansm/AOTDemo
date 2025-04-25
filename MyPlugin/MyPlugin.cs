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
