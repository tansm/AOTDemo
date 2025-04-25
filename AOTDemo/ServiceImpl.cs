using AOTDemo.Services;

namespace AOTDemo
{
    public class DemoService : IDemoService {
        private int _count = 123;

        public string query(string query, QueryArgs args) {
            return "exe:" + query + ", " + args.Name + ", " + args.Age;
        }
    }
}
