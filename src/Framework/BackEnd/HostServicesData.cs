// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Microsoft.Build.BackEnd;

/// <summary>
///  Contains the remote host object registrations transferred to a task host.
/// </summary>
/// <remarks>
///  In-process host objects and project node affinities are not part of this payload.
/// </remarks>
internal sealed class HostServicesData : ITranslatable
{
    public static HostServicesData Empty { get; } = new(registrations: []);

    private ImmutableArray<Registration> _registrations;

    /// <summary>
    ///  Initializes a new instance of the <see cref="HostServicesData"/> class.
    /// </summary>
    /// <param name="registrations">The initialized array of remote host object registrations in wire order.</param>
    public HostServicesData(ImmutableArray<Registration> registrations)
    {
        Assumed.True(!registrations.IsDefault, "The remote host object registrations must be initialized.");
        _registrations = registrations;
    }

    /// <summary>
    ///  Gets the remote host object registrations in wire order.
    /// </summary>
    public ImmutableArray<Registration> Registrations => _registrations;

    /// <summary>
    ///  Creates host services initialized with the remote registrations.
    /// </summary>
    /// <typeparam name="T">The host-services type implementing <see cref="IRemoteHostServicesInitializer"/>.</typeparam>
    /// <returns>
    ///  A new host-services instance containing the remote registrations.
    /// </returns>
    public T CreateHostServices<T>()
        where T : class, IRemoteHostServicesInitializer, new()
    {
        T hostServices = new();
        hostServices.Initialize(this);
        return hostServices;
    }

    /// <summary>
    ///  Translates the remote registrations using the existing host-services wire format.
    /// </summary>
    /// <param name="translator">The translator to use.</param>
    public void Translate(ITranslator translator)
    {
        if (translator.Mode == TranslationDirection.ReadFromStream)
        {
            int count = translator.Reader.ReadInt32();

            if (count == 0)
            {
                _registrations = [];
            }
            else
            {
                Registration[] registrations = new Registration[count];

                for (int i = 0; i < count; i++)
                {
                    registrations[i] = new Registration(
                        translator.Reader.ReadString(),
                        translator.Reader.ReadString(),
                        translator.Reader.ReadString(),
                        translator.Reader.ReadString());
                }

                _registrations = ImmutableCollectionsMarshal.AsImmutableArray(registrations);
            }
        }
        else
        {
            translator.Writer.Write(_registrations.Length);

            foreach (Registration registration in _registrations)
            {
                // These strings have no presence markers in the legacy HostServices payload.
                translator.Writer.Write(registration.ProjectFile);
                translator.Writer.Write(registration.TargetName);
                translator.Writer.Write(registration.TaskName);
                translator.Writer.Write(registration.MonikerName);
            }
        }
    }

    /// <summary>
    ///  Deserializes the remote host object registrations.
    /// </summary>
    /// <param name="translator">The translator to use.</param>
    /// <returns>
    ///  The deserialized host-services data.
    /// </returns>
    public static HostServicesData FactoryForDeserialization(ITranslator translator)
    {
        HostServicesData data = new([]);
        data.Translate(translator);
        return data;
    }

    /// <summary>
    ///  Identifies a remote host object by its project, target, task, and COM moniker.
    /// </summary>
    /// <param name="ProjectFile">The project file associated with the host object.</param>
    /// <param name="TargetName">The target associated with the host object.</param>
    /// <param name="TaskName">The task associated with the host object.</param>
    /// <param name="MonikerName">The host object's registration name in the Running Object Table.</param>
    public readonly record struct Registration(string ProjectFile, string TargetName, string TaskName, string MonikerName);
}
