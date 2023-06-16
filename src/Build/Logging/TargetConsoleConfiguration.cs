// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable
using System;

namespace Microsoft.Build.BackEnd.Logging;

/// <summary>
/// Console configuration of target Console at which we will render output.
/// It is supposed to be Console from other process to which output from this process will be redirected.
/// </summary>
internal class TargetConsoleConfiguration : IConsoleConfiguration, ITranslatable
{
    private int _bufferWidth;
    private bool _acceptAnsiColorCodes;
    private bool _outputIsScreen;
    private ConsoleColor _backgroundColor;

    public TargetConsoleConfiguration(int bufferWidth, bool acceptAnsiColorCodes, bool outputIsScreen, ConsoleColor backgroundColor)
    {
        _bufferWidth = bufferWidth;
        _acceptAnsiColorCodes = acceptAnsiColorCodes;
        _outputIsScreen = outputIsScreen;
        _backgroundColor = backgroundColor;
    }

    /// <summary>
    /// Constructor for deserialization
    /// </summary>
    private TargetConsoleConfiguration()
    {
    }

    public int BufferWidth => _bufferWidth;

    public bool AcceptAnsiColorCodes => _acceptAnsiColorCodes;

    public bool OutputIsScreen => _outputIsScreen;

    public ConsoleColor BackgroundColor => _backgroundColor;

    public void Translate(ITranslator translator)
    {
        translator.Translate(ref _bufferWidth);
        translator.Translate(ref _acceptAnsiColorCodes);
        translator.Translate(ref _outputIsScreen);
        translator.TranslateEnum(ref _backgroundColor, (int)_backgroundColor);
    }

    internal static TargetConsoleConfiguration FactoryForDeserialization(ITranslator translator)
    {
        TargetConsoleConfiguration configuration = new();
        configuration.Translate(translator);
        return configuration;
    }
}
