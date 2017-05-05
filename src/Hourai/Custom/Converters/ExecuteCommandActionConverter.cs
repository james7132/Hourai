using System;
using System.Collections.Generic;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Hourai.Custom.Converters {

public class ExecuteCommandActionConverter : IYamlTypeConverter {

  public bool Accepts(Type type) => type == typeof(ExecuteCommandAction);

  public object ReadYaml(IParser parser, Type type) {
    var executeCommand = new ExecuteCommandAction();
    var current = parser.Current;
    var scalar = current as Scalar;
    var sequenceStart = current as SequenceStart;
    if (scalar != null && !string.IsNullOrWhiteSpace(scalar.Value)) {
      executeCommand.Commands = new List<string>() { scalar.Value };
      parser.MoveNext();
    }
    else if (sequenceStart != null) {
      parser.MoveNext();
      executeCommand.Commands = new List<string>();
      while (parser.Allow<SequenceEnd>() == null) {
        executeCommand.Commands.Add(parser.Expect<Scalar>().Value);
      }
    }
    return executeCommand;
  }

  public void WriteYaml(IEmitter emitter, object value, Type type) {
    var action = value as ExecuteCommandAction;
    if (action == null)
      return;
    if (action.Commands == null || action.Commands.Count <= 0) {
      emitter.Emit(new Scalar(""));
    } else if (action.Commands.Count == 1) {
      emitter.Emit(new Scalar(action.Commands[0]));
    } else  {
      emitter.Emit(new SequenceStart(null, null, true, SequenceStyle.Block));
      foreach (var command in action.Commands) {
        emitter.Emit(new Scalar(command));
      }
      emitter.Emit(new SequenceEnd());
    }

  }

}

}
