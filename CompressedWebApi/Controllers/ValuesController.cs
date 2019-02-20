using System.Collections.Generic;
using System.Web.Http;

namespace CompressedWebApi.Controllers
{
    public class ValuesController : ApiController
    {
        public void Delete(int id) { }

        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        public string Get(int id)
        {
            return "value";
        }

        public string Post([FromBody] string value)
        {
            return $"Echo: {value}";
        }

        public void Put(int id, [FromBody] string value) { }
    }
}