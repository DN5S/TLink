using System;

namespace SamplePlugin.Core.MVU;

public interface IState : ICloneable
{
    string Id { get; init; }
    long Version { get; init; }
}