using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using ChillPatcher;
using UnityEngine;

public class HttpServer
{
    private HttpListener listener;
    private Thread thread;
    private Dictionary<string, Func<Request, Response>> getRoutes = new Dictionary<string, Func<Request, Response>>();
    private Dictionary<string, Func<Request, Response>> postRoutes = new Dictionary<string, Func<Request, Response>>();

    public HttpServer(int port = 8080)
    {
        listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
    }

    public void Start()
    {
        listener.Start();
        thread = new Thread(() =>
        {
            while (listener.IsListening)
            {
                try
                {
                    Plugin.Logger.LogInfo("[HttpServer] Waiting for a connection...");
                    var ctx = listener.GetContext();
                    ThreadPool.QueueUserWorkItem(_ => Handle(ctx));
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogError($"[HttpServer] Error: {ex.Message}");
                }
            }
        }) { IsBackground = true };
        thread.Start();
    }


    public void Stop() => listener?.Stop();

    public void Get(string path, Func<Request, Response> handler) => getRoutes[path] = handler;

    public void Post(string path, Func<Request, Response> handler) => postRoutes[path] = handler;

    private void Handle(HttpListenerContext ctx)
    {
        var req = new Request(ctx.Request);
        var res = req.Method switch
        {
            "GET" => getRoutes[req.Path](req) ?? new Response(404),
            "POST" => postRoutes[req.Path](req) ?? new Response(404),
            _ => new Response(404)
        };
        Plugin.Logger.LogInfo("[HttpFramework] Handled request: " + req.Method + " " + req.Path);
        ctx.Response.StatusCode = res.Status;
        ctx.Response.ContentType = res.Type;
        var bytes = Encoding.UTF8.GetBytes(res.Body);
        ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
        ctx.Response.Close();
    }
}

public static class Dictionary
{
    public static TValue GetOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key,
        TValue defaultValue = default)
    {
        return dict.TryGetValue(key, out var value) ? value : defaultValue;
    }
}

public class Request
{
    public string Method { get; }
    public string Path { get; }
    public Dictionary<string, string> Query { get; }
    public Json Json { get; }

    public Request(HttpListenerRequest req)
    {
        Method = req.HttpMethod;
        Path = req.Url.AbsolutePath;
        Query = ParseQuery(req.Url.Query);
        Json = req.HasEntityBody ? new Json(ReadBody(req)) : null;
    }

    private Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(query)) return result;

        foreach (var pair in query.TrimStart('?').Split('&'))
        {
            var kv = pair.Split('=');
            if (kv.Length == 2) result[Uri.UnescapeDataString(kv[0])] = Uri.UnescapeDataString(kv[1]);
        }

        return result;
    }

    private string ReadBody(HttpListenerRequest req)
    {
        using (var reader = new StreamReader(req.InputStream, req.ContentEncoding))
            return reader.ReadToEnd();
    }
}

public class Response
{
    public int Status { get; set; } = 200;
    public string Type { get; set; } = "application/json";
    public string Body { get; set; } = "";

    public Response(int status = 200, string body = "", string type = "application/json")
    {
        Status = status;
        Body = body;
        Type = type;
    }

    public static Response Ok() => Json(new { message = "ok" });
    public static Response Json(object obj) => new Response(200, JsonUtil.Serialize(obj));
    public static Response Text(string text) => new Response(200, text, "text/plain");
}

public class Json
{
    private Dictionary<string, object> data = new Dictionary<string, object>();

    public Json(string json = null)
    {
        if (!string.IsNullOrEmpty(json)) data = JsonUtil.Parse(json);
    }

    public T Get<T>(string key) => data.ContainsKey(key) ? (T)Convert.ChangeType(data[key], typeof(T)) : default(T);
    public void Set(string key, object value) => data[key] = value;
    public override string ToString() => JsonUtil.Serialize(data);
}

public static class JsonUtil
{
    public static Dictionary<string, object> Parse(string json)
    {
        var result = new Dictionary<string, object>();
        json = json.Trim().Trim('{', '}');
        if (string.IsNullOrEmpty(json)) return result;

        var pairs = Split(json, ',');
        foreach (var pair in pairs)
        {
            var colon = pair.IndexOf(':');
            if (colon == -1) continue;

            var key = pair.Substring(0, colon).Trim().Trim('"');
            var value = ParseValue(pair.Substring(colon + 1).Trim());
            result[key] = value;
        }

        return result;
    }

    public static string Serialize(object obj)
    {
        if (obj == null) return "null";
        if (obj is string) return $"\"{obj}\"";
        if (obj is bool) return obj.ToString().ToLower();
        if (obj.GetType().IsPrimitive) return obj.ToString();
        if (obj is Dictionary<string, object> dict) return SerializeDict(dict);
        return SerializeObject(obj);
    }

    private static object ParseValue(string value)
    {
        value = value.Trim();
        if (value == "null") return null;
        if (value == "true") return true;
        if (value == "false") return false;
        if (value.StartsWith("\"")) return value.Trim('"');
        if (double.TryParse(value, out double d)) return value.Contains(".") ? d : (int)d;
        return value;
    }

    private static string[] Split(string str, char separator)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var depth = 0;
        var inString = false;

        foreach (char c in str)
        {
            if (c == '"') inString = !inString;
            if (!inString && (c == '{' || c == '[')) depth++;
            if (!inString && (c == '}' || c == ']')) depth--;

            if (!inString && c == separator && depth == 0)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else current.Append(c);
        }

        if (current.Length > 0) result.Add(current.ToString());
        return result.ToArray();
    }

    private static string SerializeDict(Dictionary<string, object> dict)
    {
        var pairs = new List<string>();
        foreach (var kv in dict)
            pairs.Add($"\"{kv.Key}\":{Serialize(kv.Value)}");
        return $"{{{string.Join(",", pairs.ToArray())}}}";
    }

    private static string SerializeObject(object obj)
    {
        var pairs = new List<string>();
        var type = obj.GetType();

        foreach (var field in type.GetFields())
            if (field.IsPublic)
                pairs.Add($"\"{field.Name}\":{Serialize(field.GetValue(obj))}");

        foreach (var prop in type.GetProperties())
            if (prop.CanRead && prop.GetGetMethod().IsPublic)
                pairs.Add($"\"{prop.Name}\":{Serialize(prop.GetValue(obj, null))}");

        return $"{{{string.Join(",", pairs.ToArray())}}}";
    }
}