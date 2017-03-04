using System;
using System.Collections.Generic;
using System.Linq;

namespace Hourai {

public class Table {

  readonly Dictionary<string, Column> _table;
  public int MinimumPadding { get; set; } = 1;
  const string MissingValue = "N/A";

  public Table() {
    _table = new Dictionary<string, Column>();
  }

  public Table(int padding) : this() {
    MinimumPadding = padding;
  }

  public object this[string col, string row] {
    get {
      Column column;
      if (_table.TryGetValue(col, out column))
        return column[row];
      return MissingValue;
    }
    set {
      if (!_table.ContainsKey(col))
        _table[col] = new Column(col);
      _table[col][row] = value;
    }
  }

  private class Column {

    readonly Dictionary<string, object> _column;
    int _width = 0;
    public int Width => Math.Max(_width, MissingValue.Length);
    public IEnumerable<string> Rows => _column.Keys;
    public string Name { get; }

    public Column(string name) {
      _column = new Dictionary<string, object>();
      Name = name;
      _width = name.Length;
    }

    public object this[string row] {
      get {
        object val;
        if (_column.TryGetValue(row, out val))
          return val;
        return MissingValue;
      }
      set {
        _column[row] = value;
        _width = Math.Max(_width, value.ToString().Length);
      }
    }

    public string Padded(string row) => this[row].ToString().PadLeft(Width);
  }

  public override string ToString() {
    var rows = _table.Values.SelectMany(c => c.Rows).Distinct().OrderBy(r => r);
    var rowNameWidth = rows.Max(r => r.Length);
    var columns = _table.Values.OrderBy(c => c.Name).ToArray();
    var padding = new string(' ', MinimumPadding);
    return new string(' ', rowNameWidth + MinimumPadding) +
      columns.Select(c => c.Name.PadLeft(c.Width)).Join(padding) + "\n" +
      rows.Select(r => r.PadLeft(rowNameWidth) + padding + columns.Select(c => c.Padded(r)).Join(padding)).Join("\n");
  }

}

}
