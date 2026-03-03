using TurboMediator;

namespace TurboMediator.Tests.Messages;

// ============ Request Messages ============

/// <summary>
/// A simple ping request for testing.
/// </summary>
public sealed record PingRequest : IRequest<PongResponse>;

/// <summary>
/// Response for ping request.
/// </summary>
public sealed record PongResponse(string Message);

/// <summary>
/// A request with a complex input for testing.
/// </summary>
public sealed record ComplexRequest(int Id, string Name, DateTime Date) : IRequest<ComplexResponse>;

/// <summary>
/// Complex response for testing.
/// </summary>
public sealed record ComplexResponse(int ProcessedId, string ProcessedName, bool Success);

// ============ Command Messages ============

/// <summary>
/// A command that creates a resource.
/// </summary>
public sealed record CreateItemCommand(string Name, decimal Price) : ICommand<CreateItemResult>;

/// <summary>
/// Result for create item command.
/// </summary>
public sealed record CreateItemResult(Guid Id, string Name, decimal Price, DateTime CreatedAt);

/// <summary>
/// A command that returns Unit (void).
/// </summary>
public sealed record DeleteItemCommand(Guid Id) : ICommand<Unit>;

// ============ Query Messages ============

/// <summary>
/// A query to find an item by id.
/// </summary>
public sealed record GetItemByIdQuery(Guid Id) : IQuery<ItemDto?>;

/// <summary>
/// A query to list all items.
/// </summary>
public sealed record GetAllItemsQuery : IQuery<IReadOnlyList<ItemDto>>;

/// <summary>
/// Item data transfer object.
/// </summary>
public sealed record ItemDto(Guid Id, string Name, decimal Price);

// ============ Notification Messages ============

/// <summary>
/// Notification when an item is created.
/// </summary>
public sealed record ItemCreatedNotification(Guid Id, string Name) : INotification;

/// <summary>
/// Notification when an item is deleted.
/// </summary>
public sealed record ItemDeletedNotification(Guid Id) : INotification;
