using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text;
using Microsoft.VisualBasic.FileIO;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace TargetSelector;

public class jsonhelper
{
  public static class JsonHelper
  {
    public static JsonSerializerSettings Settings = new JsonSerializerSettings()
    {
      Formatting = Formatting.Indented,
      TypeNameHandling = TypeNameHandling.Auto
    };
    public static JsonSerializerSettings DeserializeSettings = new JsonSerializerSettings()
    {
      Formatting = Formatting.Indented,
      TypeNameHandling = TypeNameHandling.Auto,
    };

    public static string ToJson(object obj) => JsonConvert.SerializeObject(obj, JsonHelper.Settings);

    public static byte[] ToBytes(object obj) => Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(obj, JsonHelper.Settings));

    public static T FromJson<T>(string str) => JsonConvert.DeserializeObject<T>(str, JsonHelper.DeserializeSettings);

    public static T FromBytes<T>(byte[] bytes) => JsonConvert.DeserializeObject<T>(JsonHelper.Bytes2Json(bytes), JsonHelper.DeserializeSettings);

    public static string Bytes2Json(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    public static object FromJson(Type type, string str)
    {
      try
      {
        return JsonConvert.DeserializeObject(str, type, JsonHelper.Settings);
      }
      catch (Exception ex)
      {
        DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(1, 2);
        interpolatedStringHandler.AppendFormatted(str);
        interpolatedStringHandler.AppendLiteral("\n");
        interpolatedStringHandler.AppendFormatted<Exception>(ex);
        throw new Exception(interpolatedStringHandler.ToStringAndClear());
      }
    }
  }
}

