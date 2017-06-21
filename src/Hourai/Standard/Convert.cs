using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnitsNet;
using UnitsNet.Units;

namespace Hourai {

  public interface IConverter {

    object Convert(string src, string target);

  }

  public class TempConverter : IConverter {

    const char AdditionalCharacter = '°';
    static string[] Replacements = new[] {"F", "C"};

    UnitConverter<Temperature, TemperatureUnit> _baseConverter;

    public TempConverter() {
      _baseConverter = new UnitConverter<Temperature, TemperatureUnit>();
    }

    string ProcessInput(string val) {
      foreach(var replacement in Replacements)
        val = val.Replace(replacement, AdditionalCharacter + replacement);
      return val;
    }

    public object Convert(string src, string targetUnit) {
      src = ProcessInput(src);
      targetUnit = ProcessInput(targetUnit);
      return _baseConverter.Convert(src, targetUnit);
    }

  }

  public class UnitConverter<T, TUnit> : IConverter {

    MethodInfo _parser;
    MethodInfo _enumParser;
    MethodInfo _strFun;

    public UnitConverter() {
      var type = typeof(T);
      _parser = type.GetMethod("Parse", new[] {typeof(string)});
      _enumParser = type.GetMethod("ParseUnit", new[] {typeof(string)});
      _strFun = type.GetMethod("ToString", new[] {typeof(TUnit)});
    }

    public object Convert(string src, string targetUnit) {
      var value = _parser.Invoke(null, new object[] {src});
      var unit = _enumParser.Invoke(null, new object[] {targetUnit});
      if (value == null || unit == null)
        return null;
      return _strFun.Invoke(value, new object[] {unit});
    }

  }

  public class UnitConversionService {

    static UnitConversionService() {
      var assembly = typeof(Mass).GetTypeInfo().Assembly;
      System.Console.WriteLine(assembly.ToString());
      var converterType = typeof(UnitConverter<object, object>);
      converterType = converterType.GetGenericTypeDefinition();
      var converters = new List<IConverter>() { new TempConverter() };
      foreach(var type in assembly.GetTypes()) {
        var unitName = string.Format("UnitsNet.Units.{0}Unit", type.Name);
        var unitType = assembly.GetType(unitName, false);
        if (unitType == null)
          continue;
        var genericType = converterType.MakeGenericType(new[] {type, unitType});
        converters.Add((IConverter)Activator.CreateInstance(genericType));
      }
      _converters = converters.ToArray();
    }

    static readonly IConverter[] _converters;

    public static async Task<object> ConvertAsync(string src, string targetUnit) {
      var conversionResults =
          await Task.WhenAll(_converters.Select(conv => Task.Run(() => {
              try {
                return conv.Convert(src, targetUnit);
              } catch {
                return null;
              }
            })));
      return conversionResults.Where(result => result != null).FirstOrDefault();
    }


  }

}
