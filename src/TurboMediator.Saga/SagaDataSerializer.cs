namespace TurboMediator.Saga;

/// <summary>
/// Delegate for serializing saga data.
/// </summary>
/// <typeparam name="T">The data type.</typeparam>
public delegate string SagaDataSerializer<in T>(T data);
