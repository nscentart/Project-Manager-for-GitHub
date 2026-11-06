using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class Response {
    public int Code;
    public object Json;
    public Dictionary<string, object> D { get { return Json as Dictionary<string, object> ?? new Dictionary<string, object>(); } }
    public IEnumerable<Dictionary<string, object>> L { get { return Json is IEnumerable && !(Json is string) && !(Json is IDictionary) ? ((IEnumerable)Json).Cast<object>().OfType<Dictionary<string, object>>() : new Dictionary<string, object>[0]; } }
}
