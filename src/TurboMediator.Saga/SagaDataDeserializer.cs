namespace TurboMediator.Saga;

/// <summary>
/// Delegate for deserializing saga data.
/// </summary>
/// <typeparam name="T">The data type.</typeparam>
public delegate T SagaDataDeserializer<out T>(string data);
