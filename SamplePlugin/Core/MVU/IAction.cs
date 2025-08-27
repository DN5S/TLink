using System;

namespace SamplePlugin.Core.MVU;

public interface IAction
{
    string Type { get; }
    DateTime Timestamp { get; }
}