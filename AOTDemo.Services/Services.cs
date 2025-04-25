using System.ComponentModel;

namespace AOTDemo.Services {
    
    public class QueryArgs {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; } = 0;
    }

    // 演示可以被外部调用的服务
    public interface IDemoService {
        string query(string query, QueryArgs args);
    }

    public interface IPlugin  {
        int Run();
    }
}
