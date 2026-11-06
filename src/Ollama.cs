using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

public static class Ollama {
    const string Base = "http://127.0.0.1:11434";
    static readonly string[] Favorites = { "qwen3.6:27b", "qwen2.5vl:7b-q8_0", "qwen3.6:35b" };

    static object Call(string path, object body, int seconds) {
        var req = (HttpWebRequest)WebRequest.Create(Base + path);
        req.Timeout = req.ReadWriteTimeout = seconds * 1000;
        req.Proxy = null;
        if (body != null) {
            req.Method = "POST";
            req.ContentType = "application/json";
            var b = Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(body));
            using (var s = req.GetRequestStream()) s.Write(b, 0, b.Length);
        }
        try {
            using (var r = req.GetResponse()) using (var sr = new StreamReader(r.GetResponseStream(), Encoding.UTF8))
                return new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.DeserializeObject(sr.ReadToEnd());
        } catch (WebException ex) {
            if (ex.Response == null) throw new Exception(Language.T("ollama.ollama_not_running_open_try"));
            throw new Exception(Language.T("ollama.ollama_answered_error", "part", (int)((HttpWebResponse)ex.Response).StatusCode));
        }
    }

    public static List<string> Models() {
        var d = (Dictionary<string, object>)Call("/api/tags", null, 5);
        return ((IEnumerable)d["models"]).Cast<Dictionary<string, object>>().Select(m => Convert.ToString(m["name"])).ToList();
    }

    public static string Choose(List<string> models, string wanted) {
        if (!string.IsNullOrEmpty(wanted) && models.Contains(wanted)) return wanted;
        foreach (var p in Favorites) if (models.Contains(p)) return p;
        return models.FirstOrDefault();
    }

    public static string Summarize(string model, string category, string title, string text) {
        string data = Language.Fixed("ollama.data", "title", title, "text", text).Replace("<<<", "< < <").Replace(">>>", "> > >");
        if (data.Length > 12000) data = data.Substring(0, 12000) + Language.Fixed("ollama.cut");
        string task = category == "log"
            ? Language.Fixed("ollama.task_log", "language", Language.NameForModel())
            : Language.Fixed("ollama.task_issue", "category", Language.Fixed("category." + category.Replace(' ', '_')), "language", Language.NameForModel());
        string prompt = Language.Fixed("ollama.prompt", "task", task, "data", data);
        var body = new Dictionary<string, object> {
            { "model", model }, { "prompt", prompt }, { "stream", false }, { "think", false },
            { "options", new Dictionary<string, object> { { "temperature", 0 }, { "num_predict", 200 } } } };
        var d = (Dictionary<string, object>)Call("/api/generate", body, 300);
        string r = Convert.ToString(d["response"]);
        r = System.Text.RegularExpressions.Regex.Replace(r, @"<think>[\s\S]*?</think>", "").Trim();
        return r.Length > 600 ? r.Substring(0, 600) + "..." : r;
    }
}
